using System;
using System.IO;
using Sandbox;

namespace Editor.UnrealImporter;

/// <summary>
/// Output texture filenames (no path) for a processed material, or null where absent.
/// </summary>
public class ProcessedTextures
{
	public string Color;
	public string Alpha;
	public string Normal;
	public string Roughness;
	public string Metallic;
	public string Ao;
	public string Emissive;
	public string TintMask;

	/// <summary>Displacement/height map. Unused by complex.shader; terrain + decal resources want it.</summary>
	public string Height;

	/// <summary>Packed R=Roughness G=Metal B=Occlusion, for decal resources (which take one RMO map).</summary>
	public string RoughMetalOcclusion;

	/// <summary>Grayscale emissive mask extracted from the albedo's alpha (opaque materials with emissive params).</summary>
	public string SelfIllumMask;
}

/// <summary>What the albedo's alpha channel means for this material - decided from the Unreal blend mode.</summary>
public enum AlphaRole
{
	/// <summary>UE blends/masks with it - extract as a translucency/alpha-test map.</summary>
	Translucency,
	/// <summary>Opaque material with emissive params - the alpha is a self-illum mask.</summary>
	SelfIllum,
	/// <summary>Opaque, no emissive - the alpha packs something we can't interpret; ignore it.</summary>
	Ignore,
}

/// <summary>
/// Turns Unreal's raw exported textures into sbox-ready ones using sbox's Bitmap:
///  - splits RMA (R=roughness, G=metallic, B=ao) into separate grayscale maps
///  - flips the normal's green channel (Unreal DirectX -> sbox OpenGL)
///  - extracts the albedo's alpha to a separate map
///  - writes everything as &lt;base&gt;_&lt;role&gt;.png (lowercase, no dots)
/// </summary>
public static class TextureProcessor
{
	/// <param name="packRmo">
	/// Emit one packed R=Rough G=Metal B=AO map instead of three grayscale ones. Decal
	/// resources take a single RMO texture, so splitting and re-packing would be lossy churn.
	/// </param>
	/// <param name="wantHeight">Also process the displacement map (terrain + decal resources use it).</param>
	/// <param name="maxTextureSize">
	/// Downscale anything larger than this on the longest edge (0 = keep the source size).
	/// Applied at LOAD time, so the channel splits and per-pixel passes below run on the
	/// smaller bitmap too - a 4K pack imports several times faster at 1K.
	/// </param>
	public static ProcessedTextures Process( ManifestMaterial mat, string stagingDir, string outputTextureDir, string baseName, AlphaRole alphaRole = AlphaRole.Translucency, bool packRmo = false, bool wantHeight = false, int maxTextureSize = 0 )
	{
		Directory.CreateDirectory( outputTextureDir );
		var result = new ProcessedTextures();

		// --- Opacity (dedicated map) ---
		// Cutout foliage and thatch ship their mask as its OWN texture and leave the albedo
		// fully opaque, so there is no alpha for the colour block below to extract. A texture
		// Unreal explicitly bound to an opacity parameter wins over the albedo's alpha; done
		// first so that alpha still serves as the fallback when this map is missing.
		if ( alphaRole == AlphaRole.Translucency && !string.IsNullOrEmpty( mat.Opacity ) )
		{
			using var opacity = Load( stagingDir, mat.Opacity, maxTextureSize );
			if ( opacity is not null )
				result.Alpha = Save( ExtractChannel( opacity, DominantChannel( opacity, includeAlpha: true ) ), outputTextureDir, baseName, "alpha", dispose: true );
		}

		// --- Color (+ alpha) ---
		if ( !string.IsNullOrEmpty( mat.Alb ) )
		{
			using var alb = Load( stagingDir, mat.Alb, maxTextureSize );
			if ( alb is not null )
			{
				// Export the albedo UNTOUCHED. Fab/Megascans albedos already contain the final
				// colours; the material's tint mask + tint colours are an OPTIONAL runtime-recolour
				// system (team colours / variants). Baking them here double-colours and corrupts
				// the result, so we keep the albedo pristine and leave tint inert in the vmat.
				result.Color = Save( alb, outputTextureDir, baseName, "color" );

				if ( result.Alpha is null && !alb.IsOpaque() && alphaRole != AlphaRole.Ignore )
				{
					if ( alphaRole == AlphaRole.SelfIllum )
						result.SelfIllumMask = Save( ExtractAlpha( alb ), outputTextureDir, baseName, "selfillum", dispose: true );
					else
						result.Alpha = Save( ExtractAlpha( alb ), outputTextureDir, baseName, "alpha", dispose: true );
				}
			}
		}

		// --- Normal (flip green) ---
		if ( !string.IsNullOrEmpty( mat.Nrm ) )
		{
			using var nrm = Load( stagingDir, mat.Nrm, maxTextureSize );
			if ( nrm is not null )
				result.Normal = Save( FlipGreen( nrm ), outputTextureDir, baseName, "normal", dispose: true );
		}

		// --- Packed RMA/ORM -> roughness / metallic / ao (or one repacked RMO) ---
		if ( !string.IsNullOrEmpty( mat.Rma ) )
		{
			using var rma = Load( stagingDir, mat.Rma, maxTextureSize );
			if ( rma is not null )
			{
				var (rough, metal, ao) = RmaChannels( mat.RmaOrder );

				if ( packRmo )
					result.RoughMetalOcclusion = Save( Reorder( rma, rough, metal, ao ), outputTextureDir, baseName, "rmo", dispose: true );
				else
				{
					result.Roughness = Save( ExtractChannel( rma, rough ), outputTextureDir, baseName, "roughness", dispose: true );
					result.Metallic = Save( ExtractChannel( rma, metal ), outputTextureDir, baseName, "metallic", dispose: true );
					result.Ao = Save( ExtractChannel( rma, ao ), outputTextureDir, baseName, "ao", dispose: true );
				}
			}
		}

		// --- Explicit single-channel maps (override RMA-derived if both somehow present) ---
		ProcessSingle( mat.Rough, stagingDir, outputTextureDir, baseName, "roughness", ref result.Roughness, maxTextureSize );
		ProcessSingle( mat.Metal, stagingDir, outputTextureDir, baseName, "metallic", ref result.Metallic, maxTextureSize );
		ProcessSingle( mat.Ao, stagingDir, outputTextureDir, baseName, "ao", ref result.Ao, maxTextureSize );
		ProcessSingle( mat.Emissive, stagingDir, outputTextureDir, baseName, "emissive", ref result.Emissive, maxTextureSize );

		if ( wantHeight )
			ProcessSingle( mat.Height, stagingDir, outputTextureDir, baseName, "height", ref result.Height, maxTextureSize );

		// A material with separate maps still owes a decal one packed RMO - build it from
		// whichever of the three exist (missing channels stay black).
		if ( packRmo && result.RoughMetalOcclusion is null && (result.Roughness ?? result.Metallic ?? result.Ao) is not null )
		{
			var packed = Combine( outputTextureDir, result.Roughness, result.Metallic, result.Ao );
			if ( packed is not null )
				result.RoughMetalOcclusion = Save( packed, outputTextureDir, baseName, "rmo", dispose: true );
		}

		// --- Tint mask (grayscale) - export the populated channel so it can drive optional
		// runtime tinting. Masks are single-channel but the data isn't always in R (this ATV
		// mask lives in B), so pick whichever channel actually carries data.
		if ( !string.IsNullOrEmpty( mat.TintMask ) )
		{
			using var mask = Load( stagingDir, mat.TintMask, maxTextureSize );
			if ( mask is not null )
				result.TintMask = Save( ExtractChannel( mask, DominantChannel( mask ) ), outputTextureDir, baseName, "tintmask", dispose: true );
		}

		return result;
	}

	/// <summary>
	/// Which channel index (0=R, 1=G, 2=B) holds roughness / metalness / AO for a packed
	/// mask, from the manifest's layout name. Fab ships _RMA, Megascans ships _ORM with the
	/// exact same look but a different order - splitting one as the other swaps roughness
	/// and AO, which reads as a flat, wrongly-shiny surface rather than an obvious error.
	/// </summary>
	static (int rough, int metal, int ao) RmaChannels( string order ) => (order ?? "rma").ToLowerInvariant() switch
	{
		// "aorm" is "orm" spelled out - the leading A is the occlusion the O already names.
		// Read as plain RMA it binds the ROUGHNESS map as metalness (a near-white metal mask)
		// and the empty metal channel as AO (fully black), which wrecks the lighting.
		"orm" or "arm" or "aorm" => (1, 2, 0),
		"mra" => (1, 0, 2),
		_ => (0, 1, 2),
	};

	static void ProcessSingle( string rel, string stagingDir, string outDir, string baseName, string role, ref string slot, int maxSize = 0 )
	{
		if ( string.IsNullOrEmpty( rel ) )
			return;

		using var bmp = Load( stagingDir, rel, maxSize );
		if ( bmp is not null )
			slot = Save( bmp, outDir, baseName, role );
	}

	static Bitmap Load( string stagingDir, string relPath, int maxSize = 0 )
	{
		var abs = Path.Combine( stagingDir, relPath.Replace( '/', Path.DirectorySeparatorChar ) );
		if ( !File.Exists( abs ) )
			return null;

		var bmp = Bitmap.CreateFromBytes( File.ReadAllBytes( abs ) );
		if ( bmp is null || !bmp.IsValid )
			return null;

		return Downscale( bmp, maxSize );
	}

	/// <summary>
	/// Shrink a bitmap so neither edge exceeds maxSize, keeping its aspect ratio. Returns the
	/// original when it already fits (or when maxSize is 0), so the caller always owns exactly
	/// one bitmap. Never upscales - the cap is a ceiling, not a target.
	/// </summary>
	static Bitmap Downscale( Bitmap bmp, int maxSize )
	{
		if ( maxSize <= 0 || (bmp.Width <= maxSize && bmp.Height <= maxSize) )
			return bmp;

		float scale = maxSize / (float)Math.Max( bmp.Width, bmp.Height );
		int w = Math.Max( 1, (int)MathF.Round( bmp.Width * scale ) );
		int h = Math.Max( 1, (int)MathF.Round( bmp.Height * scale ) );

		var resized = bmp.Resize( w, h );
		bmp.Dispose();
		return resized;
	}

	static string Save( Bitmap bmp, string outDir, string baseName, string role, bool dispose = false )
	{
		var fileName = $"{baseName}_{role}.png";
		File.WriteAllBytes( Path.Combine( outDir, fileName ), bmp.ToPng() );
		if ( dispose )
			bmp.Dispose();

		return fileName;
	}

	/// <summary>
	/// Index (0=R,1=G,2=B,3=A) of the channel carrying the mask data (widest value range).
	/// includeAlpha lets alpha win - opacity maps are sometimes white RGB with the cutout in
	/// alpha, whereas a tint mask never lives there and would only be spoiled by considering it.
	/// </summary>
	static int DominantChannel( Bitmap src, bool includeAlpha = false )
	{
		var px = src.GetPixels();
		var min = new[] { 1f, 1f, 1f, 1f };
		var max = new[] { 0f, 0f, 0f, 0f };

		// Sample sparsely - masks are large and uniform enough that this is plenty.
		int step = Math.Max( 1, px.Length / 100000 );
		for ( int i = 0; i < px.Length; i += step )
		{
			var c = px[i];
			var v = new[] { c.r, c.g, c.b, c.a };
			for ( int n = 0; n < 4; n++ )
			{
				if ( v[n] < min[n] ) min[n] = v[n];
				if ( v[n] > max[n] ) max[n] = v[n];
			}
		}

		int count = includeAlpha ? 4 : 3;
		int best = 0;
		for ( int n = 1; n < count; n++ )
		{
			if ( max[n] - min[n] > max[best] - min[best] )
				best = n;
		}

		return best;
	}

	/// <summary>
	/// New bitmap with the source channels moved into R=Rough, G=Metal, B=AO order.
	/// A source that's already RMA comes out unchanged.
	/// </summary>
	static Bitmap Reorder( Bitmap src, int rough, int metal, int ao )
	{
		var pixels = src.GetPixels();
		for ( int i = 0; i < pixels.Length; i++ )
		{
			var c = pixels[i];
			float Ch( int n ) => n == 0 ? c.r : n == 1 ? c.g : c.b;
			pixels[i] = new Color( Ch( rough ), Ch( metal ), Ch( ao ), 1f );
		}

		var bmp = new Bitmap( src.Width, src.Height );
		bmp.SetPixels( pixels );
		return bmp;
	}

	/// <summary>
	/// Pack three already-written grayscale maps into one RMO bitmap. Null slots stay black.
	/// Returns null unless every supplied map shares the same dimensions - rescaling here
	/// would be guesswork, and a mismatched pack is worse than none.
	/// </summary>
	static Bitmap Combine( string dir, string roughFile, string metalFile, string aoFile )
	{
		Bitmap Read( string f ) => string.IsNullOrEmpty( f ) ? null : Load( dir, f );

		using var r = Read( roughFile );
		using var m = Read( metalFile );
		using var a = Read( aoFile );

		var any = r ?? m ?? a;
		if ( any is null )
			return null;

		foreach ( var b in new[] { r, m, a } )
		{
			if ( b is not null && (b.Width != any.Width || b.Height != any.Height) )
				return null;
		}

		var rp = r?.GetPixels();
		var mp = m?.GetPixels();
		var ap = a?.GetPixels();
		var outPixels = new Color[any.Width * any.Height];

		for ( int i = 0; i < outPixels.Length; i++ )
			outPixels[i] = new Color( rp?[i].r ?? 0f, mp?[i].r ?? 0f, ap?[i].r ?? 0f, 1f );

		var bmp = new Bitmap( any.Width, any.Height );
		bmp.SetPixels( outPixels );
		return bmp;
	}

	/// <summary>New grayscale bitmap from one channel (0=R, 1=G, 2=B, 3=A).</summary>
	static Bitmap ExtractChannel( Bitmap src, int channel )
	{
		var pixels = src.GetPixels();
		for ( int i = 0; i < pixels.Length; i++ )
		{
			var c = pixels[i];
			float v = channel == 0 ? c.r : channel == 1 ? c.g : channel == 2 ? c.b : c.a;
			pixels[i] = new Color( v, v, v, 1f );
		}

		var bmp = new Bitmap( src.Width, src.Height );
		bmp.SetPixels( pixels );
		return bmp;
	}

	/// <summary>New bitmap with the green channel inverted (DirectX -> OpenGL normals).</summary>
	static Bitmap FlipGreen( Bitmap src )
	{
		var pixels = src.GetPixels();
		for ( int i = 0; i < pixels.Length; i++ )
		{
			var c = pixels[i];
			pixels[i] = new Color( c.r, 1f - c.g, c.b, c.a );
		}

		var bmp = new Bitmap( src.Width, src.Height );
		bmp.SetPixels( pixels );
		return bmp;
	}

	/// <summary>New grayscale bitmap holding the source alpha.</summary>
	static Bitmap ExtractAlpha( Bitmap src )
	{
		var pixels = src.GetPixels();
		for ( int i = 0; i < pixels.Length; i++ )
		{
			float a = pixels[i].a;
			pixels[i] = new Color( a, a, a, 1f );
		}

		var bmp = new Bitmap( src.Width, src.Height );
		bmp.SetPixels( pixels );
		return bmp;
	}
}
