// Wege (C16): Straßen und Pfade als eigener Durchgang über dem Boden.
//
// Der Nutzer (2026-10-16): „in der Feudalzeit sehen die "Feldstrassen" nicht wirklich wie
// strassen aus“ und „es muss schon zum rest des games passen und nach "strasse" oder Pfad
// aussehen“. Vorher malte der Bodenshader die Wegkacheln weich als Fläche - das gab Flecken.
//
// Der SpriteBatch zeichnet die Wegkarte (RTSGameplayScreen.RoadDrawData, ein Texel je
// Kachel: rot 1 = Weg, grün 1 = Gebäude) über die ganze Karte. Der Shader legt durch jede
// Wegkachel Linien zwischen den Kantenmitten zu ihren Wegnachbarn: geradeaus durch, in
// Kurven schräg abgeschnitten, in Sackgassen bis zur Mitte. So werden Treppen aus Kacheln
// zu Diagonalen und Ecken zu Kurven; die Linien bekommen eine feste Breite und eine Kante.
// Je Zeitalter ein Aussehen: Pflasterstraße mit Randsteinen, Erdweg mit Fahrspuren,
// gewölbte Schotterstraße. Die Wegkarte ist vierfach zusammenhängend (RoadDrawData füllt
// schräge Lücken auf), deshalb genügen gerade Nachbarn.

#if OPENGL
	#define SV_POSITION POSITION
	#define PS_SHADERMODEL ps_3_0
#else
	#define PS_SHADERMODEL ps_4_0
#endif

float2 MapTiles;      // Kartengröße in Kacheln
float RoadScale;      // Wegbild-Koordinate je Kachel
float HalfWidth;      // halbe Wegbreite in Kacheln
float Curb;           // 0..1 Randsteine (Pflaster)
float Ruts;           // 0..1 Fahrspuren (Erdweg)
float Crown;          // 0..1 Wölbung: Mitte heller, Rand dunkler (Schotter)
float Fray;           // 0..1 wie stark die Kante ausfranst
float3 VergeColor;    // ausgetretener Saum neben dem Weg
float3 Tint;          // Farbton der Oberfläche, damit sie zur gemalten Wiese passt

Texture2D SpriteTexture;   // die Wegkarte, setzt der SpriteBatch
sampler2D MapSampler = sampler_state
{
	Texture = <SpriteTexture>;
	AddressU = Clamp;
	AddressV = Clamp;
	MagFilter = Point;
	MinFilter = Point;
	MipFilter = Point;
};

Texture2D RoadTexture;
sampler2D RoadSampler = sampler_state
{
	Texture = <RoadTexture>;
	AddressU = Wrap;
	AddressV = Wrap;
	MagFilter = Linear;
	MinFilter = Linear;
	MipFilter = Linear;
};

Texture2D NoiseTexture;
sampler2D NoiseSampler = sampler_state
{
	Texture = <NoiseTexture>;
	AddressU = Wrap;
	AddressV = Wrap;
	MagFilter = Linear;
	MinFilter = Linear;
	MipFilter = Linear;
};

struct VertexShaderOutput
{
	float4 Position : SV_POSITION;
	float4 Color : COLOR0;
	float2 TextureCoordinates : TEXCOORD0;
};

// Wegkarte an der Kachel cell: rot Weg, grün Gebäude
float2 MapAt(float2 cell)
{
	return tex2Dlod(MapSampler, float4((cell + 0.5) / MapTiles, 0.0, 0.0)).rg;
}

// Abstand von p zur Strecke a-b, auch zu einem Punkt, wenn a == b.
// VERTRAG: pa = p - a, ba = b - a; t = saturate(dot(pa, ba) / (dot(ba, ba) + 1e-5));
// Rückgabe length(pa - ba * t). Kein if, keine Schleife.
float Seg(float2 p, float2 a, float2 b)
{
	float2 pa = p - a;
	float2 ba = b - a;
	float t = saturate(dot(pa, ba) / (dot(ba, ba) + 1e-5));
	return length(pa - ba * t);
}

// Kleinster Abstand von p zu den Linien der Kachel cell, wenn sie Weg ist (s = 1); e, w, n, so
// sagen, ob der Nachbar rechts, links, oben, unten Weg ist (oben auch: Gebäude, dort führt der
// Weg zur Tür). Alles in Kachelkoordinaten.
// VERTRAG (ohne if, jede Strecke per d = min(d, lerp(10.0, Seg(...), an))):
// - m = cell + 0.5 (Mitte), pE = m + float2(0.5, 0), pW = m - float2(0.5, 0),
//   pN = m - float2(0, 0.5), pS = m + float2(0, 0.5) (Kantenmitten); k = e + w + n + so; d = 10.
// - Höchstens ein Nachbar: Strecke von m nach ende = m + float2(e - w, so - n) * 0.5,
//   an = s * step(k, 1.5) (allein: ende == m, ein Punkt).
// - Geradeaus: pW-pE mit an = s * e * w; pN-pS mit an = s * n * so.
// - Kurven, schräg von Kantenmitte zu Kantenmitte: pE-pN (s * e * n), pE-pS (s * e * so),
//   pW-pN (s * w * n), pW-pS (s * w * so).
// - Rückgabe d (10, wenn nichts gilt).
float CellNet(float2 p, float2 cell, float s, float e, float w, float n, float so)
{
	float2 m = cell + 0.5;
	float2 pE = m + float2(0.5, 0);
	float2 pW = m - float2(0.5, 0);
	float2 pN = m - float2(0, 0.5);
	float2 pS = m + float2(0, 0.5);
	float k = e + w + n + so;
	float d = 10.0;
	// Hoechstens ein Nachbar: Linie von der Mitte zur Kantenmitte (allein: ein Punkt)
	d = min(d, lerp(10.0, Seg(p, m, m + float2(e - w, so - n) * 0.5), s * step(k, 1.5)));
	// Geradeaus durch die Kachel
	d = min(d, lerp(10.0, Seg(p, pW, pE), s * e * w));
	d = min(d, lerp(10.0, Seg(p, pN, pS), s * n * so));
	// Kurven: schlaeg von Kantenmitte zu Kantenmitte
	d = min(d, lerp(10.0, Seg(p, pE, pN), s * e * n));
	d = min(d, lerp(10.0, Seg(p, pE, pS), s * e * so));
	d = min(d, lerp(10.0, Seg(p, pW, pN), s * w * n));
	d = min(d, lerp(10.0, Seg(p, pW, pS), s * w * so));
	return d;
}

// VERTRAG MainPS, in dieser Reihenfolge:
// 1. tile = TextureCoordinates * MapTiles; corner = floor(tile + 0.5) (die nächste Kachelecke);
//    b = corner - 1.0 (links oben im 2x2-Block um die Ecke - nur dessen Linien reichen bis hier).
// 2. Wegkarte lesen (MapAt): m00 = b, m10 = b + (1, 0), m01 = b + (0, 1), m11 = b + (1, 1),
//    n00 = b + (0, -1), n10 = b + (1, -1) (je .rg); w00 = b + (-1, 0), e10 = b + (2, 0),
//    w01 = b + (-1, 1), e11 = b + (2, 1), s01 = b + (0, 2), s11 = b + (1, 2) (je .r).
// 3. d = CellNet(tile, b, m00.r, m10.r, w00, max(n00.r, n00.g), m01.r), dann min mit
//    CellNet(tile, b + (1, 0), m10.r, e10, m00.r, max(n10.r, n10.g), m11.r),
//    CellNet(tile, b + (0, 1), m01.r, m11.r, w01, max(m00.r, m00.g), s01),
//    CellNet(tile, b + (1, 1), m11.r, e11, m01.r, max(m10.r, m10.g), s11); und vier Wegkacheln
//    im Quadrat füllen die Fläche dazwischen:
//    d = min(d, lerp(10.0, length(tile - corner) - 0.3, m00.r * m10.r * m01.r * m11.r)).
// 4. nm = tex2D(NoiseSampler, tile * 0.042 + 0.31), nf = tex2D(NoiseSampler, tile * 0.6 + 0.57);
//    hw = HalfWidth; de = d + ((nf.r - 0.5) * 0.10 + (nm.b - 0.5) * 0.06) * Fray;
//    aa = max(fwidth(de), 0.004); road = 1 - smoothstep(hw - aa, hw + aa, de);
//    verge = (1 - smoothstep(hw, min(hw + 0.13, 0.49), de)) * (0.55 + nf.g * 0.3).
// 5. c = tex2D(RoadSampler, tile * RoadScale).rgb * Tint * (0.9 + nm.g * 0.2);
//    Wölbung: c *= lerp(1.0, lerp(1.08, 0.82, smoothstep(0.0, hw, de)), Crown);
//    Fahrspuren: rut = 1 - smoothstep(0.02, 0.055, abs(d - hw * 0.52));
//    c = lerp(c, c * float3(0.45, 0.4, 0.36), Ruts * rut * (0.75 + nf.b * 0.25));
//    Grasnarbe dazwischen: c = lerp(c, c * float3(0.85, 1.0, 0.7), Ruts * (1 - smoothstep(0.0, 0.06, d)) * 0.6);
//    Randsteine: curb = smoothstep(hw - 0.085, hw - 0.06, de);
//    c = lerp(c, c * 0.72 + float3(0.06, 0.06, 0.05), Curb * curb);
//    Schattenkante: edge = smoothstep(hw - 0.03, hw, de); c *= 1 - edge * lerp(0.25, 0.45, Curb).
// 6. a = max(road, verge * 0.45); col = lerp(VergeColor, c, road); Rückgabe vormultipliziert
//    float4(col * a, a).
float4 MainPS(VertexShaderOutput input) : COLOR
{
	// 1. Naechste Kachelecke und der 2x2-Block links oben darum
	float2 tile = input.TextureCoordinates * MapTiles;
	float2 corner = floor(tile + 0.5);
	float2 b = corner - 1.0;
	// 2. Wegkarte lesen: m = Kachel (rg), n = ueber der Kachel (rg), Rest nur Weg (r)
	float2 m00 = MapAt(b);
	float2 m10 = MapAt(b + float2(1, 0));
	float2 m01 = MapAt(b + float2(0, 1));
	float2 m11 = MapAt(b + float2(1, 1));
	float2 n00 = MapAt(b + float2(0, -1));
	float2 n10 = MapAt(b + float2(1, -1));
	float w00 = MapAt(b + float2(-1, 0)).r;
	float e10 = MapAt(b + float2(2, 0)).r;
	float w01 = MapAt(b + float2(-1, 1)).r;
	float e11 = MapAt(b + float2(2, 1)).r;
	float s01 = MapAt(b + float2(0, 2)).r;
	float s11 = MapAt(b + float2(1, 2)).r;
	// 3. Abstand zu allen Linien der vier Kacheln; vier Wegkacheln fuellen die Flaechenluecke
	float d = CellNet(tile, b, m00.r, m10.r, w00, max(n00.r, n00.g), m01.r);
	d = min(d, CellNet(tile, b + float2(1, 0), m10.r, e10, m00.r, max(n10.r, n10.g), m11.r));
	d = min(d, CellNet(tile, b + float2(0, 1), m01.r, m11.r, w01, max(m00.r, m00.g), s01));
	d = min(d, CellNet(tile, b + float2(1, 1), m11.r, e11, m01.r, max(m10.r, m10.g), s11));
	d = min(d, lerp(10.0, length(tile - corner) - 0.3, m00.r * m10.r * m01.r * m11.r));
	// 4. Ausgefranschte Kante, Deckung und Saum
	float4 nm = tex2D(NoiseSampler, tile * 0.042 + 0.31);
	float4 nf = tex2D(NoiseSampler, tile * 0.6 + 0.57);
	float hw = HalfWidth;
	float de = d + ((nf.r - 0.5) * 0.10 + (nm.b - 0.5) * 0.06) * Fray;
	float aa = max(fwidth(de), 0.004);
	float road = 1 - smoothstep(hw - aa, hw + aa, de);
	float verge = (1 - smoothstep(hw, min(hw + 0.13, 0.49), de)) * (0.55 + nf.g * 0.3);
	// 5. Oberflaechenfarbe je Zeitalter: Wuelbung, Fahrspuren, Grasnarbe, Randsteine, Schattenkante
	float3 c = tex2D(RoadSampler, tile * RoadScale).rgb * Tint * (0.9 + nm.g * 0.2);
	c *= lerp(1.0, lerp(1.08, 0.82, smoothstep(0.0, hw, de)), Crown);
	float rut = 1 - smoothstep(0.02, 0.055, abs(d - hw * 0.52));
	c = lerp(c, c * float3(0.45, 0.4, 0.36), Ruts * rut * (0.75 + nf.b * 0.25));
	c = lerp(c, c * float3(0.85, 1.0, 0.7), Ruts * (1 - smoothstep(0.0, 0.06, d)) * 0.6);
	float curb = smoothstep(hw - 0.085, hw - 0.06, de);
	c = lerp(c, c * 0.72 + float3(0.06, 0.06, 0.05), Curb * curb);
	float edge = smoothstep(hw - 0.03, hw, de);
	c *= 1 - edge * lerp(0.25, 0.45, Curb);
	// 6. Vormultiplizierte Farbe mit Deckung
	float a = max(road, verge * 0.45);
	float3 col = lerp(VergeColor, c, road);
	return float4(col * a, a);
}

technique Wege
{
	pass P0
	{
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
};
