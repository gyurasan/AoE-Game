// Boden aus einem Guss: Gras, Sand und Wasser in einem Durchgang.
//
// Der SpriteBatch zeichnet das Steuerbild (RTSGameplayScreen.BuildGroundControl)
// über die ganze Karte. Seine Texturkoordinate mal MapTiles ist die Lage in
// Kacheln. Das Steuerbild trägt je Kachel mehrere Texel, geglättet: rot Sand,
// grün Wasser, blau die Wassertiefe - auf einer Kachelgrenze steht dort genau
// die Hälfte. Mit Rauschen verschoben werden daraus weiche, unregelmäßige
// Ufer und Strände statt Kachelkanten. Das Lebendbild (ein Texel je Kachel)
// ändert sich im Spiel: rot wie ausgetreten der Boden ist, grün Wald, blau
// ein Fischschwarm.

#if OPENGL
	#define SV_POSITION POSITION
	#define PS_SHADERMODEL ps_3_0
#else
	#define PS_SHADERMODEL ps_4_0
#endif

float2 MapTiles;      // Kartengröße in Kacheln
float Time;           // Sekunden, Takt von Wellen und Schaum
float DetailScale;    // Bodenbild-Koordinate je Kachel (TileSize * GROUND_TEXELS / Bildbreite)
float GrassScale;     // Grasbild-Koordinate je Kachel (TileSize * GRASS_TEXELS / Breite des Grasbilds)

Texture2D SpriteTexture;   // das Steuerbild, setzt der SpriteBatch
sampler2D ControlSampler = sampler_state
{
	Texture = <SpriteTexture>;
};

Texture2D LiveTexture;
sampler2D LiveSampler = sampler_state
{
	Texture = <LiveTexture>;
	AddressU = Clamp;
	AddressV = Clamp;
	MagFilter = Linear;
	MinFilter = Linear;
	MipFilter = Point;
};

Texture2D GrassTexture;
sampler2D GrassSampler = sampler_state
{
	Texture = <GrassTexture>;
	AddressU = Wrap;
	AddressV = Wrap;
	MagFilter = Linear;
	MinFilter = Linear;
	MipFilter = Linear;
};

Texture2D SandTexture;
sampler2D SandSampler = sampler_state
{
	Texture = <SandTexture>;
	AddressU = Wrap;
	AddressV = Wrap;
	MagFilter = Linear;
	MinFilter = Linear;
	MipFilter = Linear;
};

Texture2D DryGrassTexture;
sampler2D DrySampler = sampler_state
{
	Texture = <DryGrassTexture>;
	AddressU = Wrap;
	AddressV = Wrap;
	MagFilter = Linear;
	MinFilter = Linear;
	MipFilter = Linear;
};

Texture2D LushGrassTexture;
sampler2D LushSampler = sampler_state
{
	Texture = <LushGrassTexture>;
	AddressU = Wrap;
	AddressV = Wrap;
	MagFilter = Linear;
	MinFilter = Linear;
	MipFilter = Linear;
};

Texture2D FlowerGrassTexture;
sampler2D FlowerSampler = sampler_state
{
	Texture = <FlowerGrassTexture>;
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

float Luma(float3 c)
{
	return dot(c, float3(0.299, 0.587, 0.114));
}

// Wellenhöhe aus zwei Lagen Rauschen, die gegeneinander treiben. Aus einer
// gröberen Mipmap-Stufe: die feinsten Oktaven machten wurmige Glanzlinien
float WaveHeight(float2 tile)
{
	float2 a = tile * 0.12 + float2(Time * 0.016, Time * 0.006);
	float2 b = tile * 0.17 + float2(-Time * 0.010, Time * 0.013);
	return tex2Dbias(NoiseSampler, float4(a, 0.0, 1.5)).r
	     + tex2Dbias(NoiseSampler, float4(b, 0.0, 1.5)).g * 0.7;
}

// --- Wiese -------------------------------------------------------------------
// Vier Grassorten aus Qwen-Image (tools/bilder, Gruppe gras): das Grundgras
// (GrassSampler), trockenes Gras mit Erdflecken (DrySampler), dunkles Gras mit Klee
// (LushSampler) und eine Blumenwiese (FlowerSampler). Je Bildpunkt gewinnt die Sorte,
// deren Gewicht plus Halmhöhe am größten ist: an den Grenzen schieben sich die Halme
// der einen Sorte zwischen die der anderen, statt dass beide weich verschwimmen.

// Halmhöhe 0 bis 1 einer Grassorte aus der Helligkeit (Luma) ihrer Farbe c, bezogen
// auf den Mittelwert mean der Sorte: bei mittlerer Helligkeit 0.5, doppelt so hell 1,
// darüber begrenzt (saturate). So ist das dunkle Gras im Mittel nicht niedriger als
// das trockene.
float GrassHeight(float3 c, float mean)
{
	return saturate(Luma(c) / mean * 0.5);
}

// Farbe der Wiese an der Bodenbild-Koordinate g. nl, nm, nf sind das Rauschen groß
// (~12 Kacheln), mittel (~3) und fein (~0,7) wie in MainPS, forest 0 bis 1 die Nähe
// zum Wald.
// VERTRAG:
// - Grundgras zweimal: a bei g, b bei float2(-g.y, g.x) * 0.83 + float2(0.43, 0.27)
//   (um 90 Grad gedreht und etwas größer); basis = lerp(a, b, smoothstep(0.4, 0.6, nm.r))
//   mal float3(0.97, 1.07, 0.9) - frischer als das Bild. So wiederholt es sich nicht
//   sichtbar.
// - Die anderen Sorten je an eigener Stelle, damit sich ihre Muster nicht decken:
//   dry = DrySampler bei g * 0.9 + float2(0.21, 0.62), mal float3(0.74, 0.84, 0.62)
//   (gelbgrün statt Stroh); lush = LushSampler bei g * 1.1 + float2(0.71, 0.13), mal 1.3;
//   flower = FlowerSampler bei g + float2(0.37, 0.81).
// - Gewichte: wDry = smoothstep(0.8, 1.0, nl.g + (nm.b - 0.5) * 0.3) - selten, in
//   großen Flecken; wLush = max(smoothstep(0.76, 0.97, nl.b + (nm.g - 0.5) * 0.3),
//   forest * 0.9) - und immer am Wald; wFlower = smoothstep(0.7, 0.8, nm.a)
//   * smoothstep(0.35, 0.65, nl.r) * (1 - forest) - Blumen in Flecken von zwei bis vier
//   Kacheln, nur in einem Teil der Wiesen. Das Grundgras hat das feste Gewicht 0.5.
// - Überblenden nach Halmhöhe: je Sorte s = Gewicht + GrassHeight(Farbe, Mittel) * 0.35,
//   mit den Mittelwerten 0.35 (Grundgras), 0.45 (trocken), 0.25 (dunkel), 0.44 (Blumen);
//   top = das größte s; jede Sorte trägt mit max(s - (top - 0.1), 0) bei, das Ergebnis
//   ist der damit gewichtete Mittelwert der vier Farben (die stärkste trägt immer bei,
//   die Summe ist also nie 0).
// - Zuletzt eine leichte Farbschwankung über die Fläche, ohne Flecken: mal
//   lerp(float3(0.96, 1.0, 0.94), float3(1.04, 1.03, 0.98), nl.a).
float3 Meadow(float2 g, float4 nl, float4 nm, float4 nf, float forest)
{
	// Grundgras zweimal, gedreht und versetzt, fleckweise gewechselt
	float3 a = tex2D(GrassSampler, g).rgb;
	float3 b = tex2D(GrassSampler, float2(-g.y, g.x) * 0.83 + float2(0.43, 0.27)).rgb;
	float3 basis = lerp(a, b, smoothstep(0.4, 0.6, nm.r)) * float3(0.97, 1.07, 0.9);
	// Die anderen Sorten je an eigener Stelle, damit sich ihre Muster nicht decken
	float3 dry = tex2D(DrySampler, g * 0.9 + float2(0.21, 0.62)).rgb * float3(0.74, 0.84, 0.62);
	float3 lush = tex2D(LushSampler, g * 1.1 + float2(0.71, 0.13)).rgb * 1.3;
	float3 flower = tex2D(FlowerSampler, g + float2(0.37, 0.81)).rgb;
	// Gewichte: trocken selten in großen Flecken, dunkel immer am Wald, Blumen in
	// Flecken von zwei bis vier Kacheln, nur in einem Teil der Wiesen
	float wDry = smoothstep(0.8, 1.0, nl.g + (nm.b - 0.5) * 0.3);
	float wLush = max(smoothstep(0.76, 0.97, nl.b + (nm.g - 0.5) * 0.3), forest * 0.9);
	float wFlower = smoothstep(0.7, 0.8, nm.a) * smoothstep(0.35, 0.65, nl.r) * (1.0 - forest);
	// Überblenden nach Halmhöhe: die stärkste Sorte trägt immer bei
	float sBasis = 0.5 + GrassHeight(basis, 0.35) * 0.35;
	float sDry = wDry + GrassHeight(dry, 0.45) * 0.35;
	float sLush = wLush + GrassHeight(lush, 0.25) * 0.35;
	float sFlower = wFlower + GrassHeight(flower, 0.44) * 0.35;
	float top = max(max(sBasis, sDry), max(sLush, sFlower));
	float cBasis = max(sBasis - (top - 0.1), 0.0);
	float cDry = max(sDry - (top - 0.1), 0.0);
	float cLush = max(sLush - (top - 0.1), 0.0);
	float cFlower = max(sFlower - (top - 0.1), 0.0);
	float3 col = (basis * cBasis + dry * cDry + lush * cLush + flower * cFlower)
	           / (cBasis + cDry + cLush + cFlower);
	// Leichte Farbschwankung über die Fläche, ohne Flecken
	return col * lerp(float3(0.96, 1.0, 0.94), float3(1.04, 1.03, 0.98), nl.a);
}

// --- Fische ---------------------------------------------------------------
// Ein Fischschwarm lebt in seiner Kachel: Kachelkoordinaten 0 bis 1, Mitte 0.5.
// Ein Fisch ist 0,3 Kacheln lang. MainPS legt aus FishMask den dunklen Rücken und,
// versetzt, seinen Schatten auf dem Grund über das Wasser, aus FishRing helle Ringe
// an der Oberfläche. FishMask läuft achtmal je Bildpunkt (vier Fische, vier
// Schatten), und ps_3_0 erlaubt höchstens 512 Anweisungen: knapp halten, keine
// Schleifen, keine Texturzugriffe.

// Wo Fisch k (0 bis 3) des Schwarms zur Zeit t schwimmt, in Kachelkoordinaten.
// VERTRAG:
// - Jeder Fisch zieht auf einer eigenen geschlossenen, geschwungenen Bahn (etwa eine
//   Lissajous-Figur oder Acht), nicht auf einem Kreis; die Bahnen der vier Fische
//   unterscheiden sich in Größe, Tempo und Drehsinn. seed (0 bis 6,283) verschiebt
//   Phase und Lage je Schwarm.
// - Die Lage ist immer höchstens 0,28 von der Mitte (0.5, 0.5) entfernt, damit der
//   ganze Fisch in der Kachel bleibt.
// - Tempo zwischen 0,08 und 0,2 Kacheln je Sekunde und leicht schwankend - ein Fisch
//   schwimmt nicht gleichmäßig.
// - dir ist die Schwimmrichtung (die Ableitung der Bahn nach t) mit Länge 1; nie die
//   Länge 0 (dann float2(1, 0)).
float2 FishPath(float t, float seed, float k, out float2 dir)
{
	float A = 0.12 + 0.10 * frac(seed * 0.31 + k * 0.23);
	float B = 0.07 + 0.07 * frac(seed * 0.47 + k * 0.41);
	float w = 0.5 + 0.5 * frac(seed * 0.29 + k * 0.37);
	float p1 = seed + k * 1.57;
	float p2 = seed * 0.7 + k * 2.9;
	float ts = t + 0.3 * sin(0.7 * t + k);
	float2 off = float2(A * sin(w * ts + p1), B * sin(2.0 * w * ts + p2));
	float2 d = float2(A * w * cos(w * ts + p1), 2.0 * B * w * cos(2.0 * w * ts + p2));
	float ang = k * 0.8 + seed;
	float ca = cos(ang);
	float sa = sin(ang);
	off = float2(off.x * ca - off.y * sa, off.x * sa + off.y * ca);
	d = float2(d.x * ca - d.y * sa, d.x * sa + d.y * ca);
	float dl = length(d);
	dir = dl > 0.0001 ? d / dl : float2(1.0, 0.0);
	return float2(0.5, 0.5) + off;
}

// Wie sehr die Stelle p zum Fisch gehört: 0 außerhalb, 1 im Fisch, dazwischen ein
// weicher Rand von etwa 0,01 Kacheln. p ist relativ zur Fischmitte, in Kacheln; dir
// die Schwimmrichtung (Länge 1); beat der Schwanzschlag von -1 bis 1.
// VERTRAG - von oben gesehen erkennt man ihn sofort als Fisch:
// - Längsachse u = dot(p, dir) (vorn positiv), Querachse v = dot(p, float2(-dir.y, dir.x)).
// - Schwanzschlag zuerst: v um beat * 0.035 * s * s verschieben, mit
//   s = saturate((0.06 - u) / 0.22) - vorn bleibt der Körper gerade, nach hinten
//   biegt er sich immer mehr, die Schwanzflosse schlägt am weitesten aus.
// - Körper von u = -0.09 (Schwanzwurzel) bis u = 0.15 (Maul), stromlinienförmig: am
//   breitesten (halbe Breite 0,045) bei u = 0.04, vorn rund zum Maul, nach hinten
//   schmal bis halbe Breite 0,012 an der Schwanzwurzel.
// - Schwanzflosse hinter der Schwanzwurzel bis u = -0.16: fächert auf halbe Breite
//   0,05 auf und ist am Ende gegabelt (in der Mitte eine Kerbe, etwa 0,03 tief).
// - Zwei kleine Brustflossen seitlich hinter dem Kopf (ab u = 0.06), schräg nach
//   hinten abstehend, je etwa 0,03 lang.
float FishMask(float2 p, float2 dir, float beat)
{
	float u = dot(p, dir);
	float v = dot(p, float2(-dir.y, dir.x));
	float s = saturate((0.06 - u) / 0.22);
	v -= beat * 0.035 * s * s;
	float av = abs(v);
	// Koerper, u von -0.09 bis 0.15
	float q = (u - 0.04) / 0.11;
	float breite = lerp(lerp(0.012, 0.045, smoothstep(-0.09, 0.04, u)),
	                    0.045 * sqrt(max(1.0 - q * q, 0.0)), step(0.04, u));
	float koerper = smoothstep(-0.004, 0.004, breite - av)
	               * smoothstep(-0.094, -0.086, u)
	               * (1.0 - smoothstep(0.146, 0.154, u));
	// Schwanzflosse, u von -0.16 bis -0.085, am Ende gegabelt
	float flosse = lerp(0.012, 0.05, saturate((-0.09 - u) / 0.07));
	float kerbe = smoothstep(-0.134, -0.126, u) * smoothstep(0.0, 0.004, (-0.13 - u) * 1.2 - av);
	float schwanz = smoothstep(-0.004, 0.004, flosse - av)
	              * smoothstep(-0.164, -0.156, u)
	              * (1.0 - smoothstep(-0.089, -0.081, u))
	              * (1.0 - kerbe);
	// Brustflossen, je eine links und rechts
	float du = (u - 0.045) / 0.02;
	float dv = (av - 0.05) / 0.008;
	float brust = 1.0 - smoothstep(0.7, 1.0, du * du + dv * dv);
	return max(max(koerper, schwanz), brust);
}

// Ein heller Ring an der Oberfläche, wo ein Fisch nach einer Fliege schnappt: 0
// außerhalb, bis 1 auf dem Ring. f ist die Lage in der Kachel (0 bis 1), seed wie bei
// FishPath.
// VERTRAG: je Schwarm alle 6 bis 9 Sekunden (aus seed) ein Ring an einer Stelle
// höchstens 0,25 von der Mitte; die Stelle wechselt von Ring zu Ring. Der Ring wächst
// in 1,5 Sekunden von Radius 0 auf 0,2, wird dabei dünner (Ringbreite 0,02 bis 0,005)
// und verblasst; danach nichts bis zum nächsten Ring.
float FishRing(float2 f, float t, float seed)
{
	float dauer = 6.0 + 3.0 * frac(seed * 0.37);
	float z = floor(t / dauer);
	float tc = frac(t / dauer) * dauer;
	if (tc > 1.5)
		return 0.0;
	float ang = z * 2.399 + seed * 1.7;
	float2 c = float2(0.5, 0.5) + float2(sin(ang), cos(ang)) * 0.25;
	float r = tc / 1.5 * 0.2;
	float breite = 0.02 - 0.015 * tc / 1.5;
	float d = length(f - c);
	float ring = smoothstep(breite, breite * 0.4, abs(d - r));
	return ring * (1.0 - tc / 1.5);
}

float4 MainPS(VertexShaderOutput input) : COLOR
{
	float2 uv = input.TextureCoordinates;
	float4 ctrl = tex2D(ControlSampler, uv);   // r Sand, g Wasser, b Tiefe
	float4 live = tex2D(LiveSampler, uv);      // r ausgetreten, g Wald
	float2 tile = uv * MapTiles;

	// Rauschen in drei Größen: großflächig (~12 Kacheln), mittel (~3), fein (~0,7)
	float4 nl = tex2D(NoiseSampler, tile * 0.0104);
	float4 nm = tex2D(NoiseSampler, tile * 0.042 + 0.31);
	float4 nf = tex2D(NoiseSampler, tile * 0.18 + 0.57);

	// --- Gras ------------------------------------------------------------
	float2 g = tile * DetailScale;
	// Unter und neben Bäumen wächst sattes Gras im Schatten der Kronen
	float forest = smoothstep(0.25, 0.75, live.g + (nm.a - 0.5) * 0.4);
	// Grasbilder mit eigenem, feinerem Maßstab (GrassScale)
	float3 grass = Meadow(tile * GrassScale, nl, nm, nf, forest);
	// Kronenschatten, schwächer als früher
	grass = lerp(grass, grass * float3(0.7, 0.74, 0.66), forest * 0.4);

	// Trampelpfade: erst plattgetretenes, gelbliches Gras, dann nackte Erde.
	// Das Rauschen franst den Pfad aus, macht aber keinen, wo keiner ist
	float wear = saturate(live.r * 1.3 + (nf.r - 0.5) * 0.6 * saturate(live.r * 4.0));
	float3 trodden = lerp(Luma(grass).xxx, grass, 0.45) * float3(1.1, 0.98, 0.68);
	// Erde: das Sandbild entsättigt und staubig braun getönt, fleckig dunkler
	float3 soil = Luma(tex2D(SandSampler, g * 1.7).rgb) * float3(0.6, 0.51, 0.4) * (0.8 + nf.g * 0.3);
	float3 land = lerp(grass, trodden, smoothstep(0.08, 0.45, wear));
	land = lerp(land, soil, smoothstep(0.5, 0.88, wear));

	// Sanfte Bodenwellen: Licht von links oben auf großflächigem Rauschen
	float hx = tex2D(NoiseSampler, (tile + float2(0.7, 0.0)) * 0.0104).r - nl.r;
	float hy = tex2D(NoiseSampler, (tile + float2(0.0, 0.7)) * 0.0104).r - nl.r;
	land *= clamp(1.0 - (hx + hy) * 2.0, 0.88, 1.12);

	// --- Sand ------------------------------------------------------------
	float3 sand = tex2D(SandSampler, g).rgb * (0.94 + nm.g * 0.1);
	// Nass und dunkler, wo das Wasser nah ist
	float wet = smoothstep(0.25, 0.47, ctrl.g + (nf.a - 0.5) * 0.08);
	sand = lerp(sand, sand * float3(0.8, 0.76, 0.7), wet * 0.8);
	// Der Strand reicht bis unter das Wasser; zum Gras hin franst er aus
	float beach = saturate(ctrl.r + ctrl.g) + (nm.b - 0.5) * 0.35 + (nf.b - 0.5) * 0.14;
	float3 ground = lerp(land, sand, smoothstep(0.45, 0.55, beach));

	// --- Wasser ----------------------------------------------------------
	float wv = ctrl.g + (nm.a - 0.5) * 0.22 + (nf.r - 0.5) * 0.06;
	float inWater = smoothstep(0.49, 0.52, wv);
	float depth = saturate(ctrl.b * 1.2 + (wv - 0.5) * 1.6);

	float e = 0.15;
	float h0 = WaveHeight(tile);
	float2 slope = float2(WaveHeight(tile + float2(e, 0.0)) - h0,
	                      WaveHeight(tile + float2(0.0, e)) - h0) / e;
	float3 n = normalize(float3(-slope * 0.22, 1.0));
	float3 light = normalize(float3(-0.45, -0.55, 0.7));
	float diffuse = saturate(dot(n, light));
	float glint = pow(saturate(dot(n, normalize(light + float3(0.0, 0.0, 1.0)))), 30.0);

	float3 water = lerp(float3(0.2, 0.5, 0.5), float3(0.04, 0.19, 0.32), smoothstep(0.05, 0.95, depth));
	// Im Flachen scheint der sandige Grund durch
	water = lerp(sand * float3(0.62, 0.74, 0.72), water, 0.25 + 0.75 * smoothstep(0.0, 0.6, depth));
	water *= 0.82 + diffuse * 0.3;
	water += glint * float3(0.85, 0.92, 1.0) * 0.35;

	// Schaum: ein fester Saum an der Wasserlinie und Wellen, die aufs Ufer zulaufen
	float shore = 1.0 - smoothstep(0.0, 0.14, wv - 0.5);
	float wave = sin(Time * 1.4 - (wv - 0.5) * 22.0 + nm.r * 6.0) * 0.5 + 0.5;
	float foam = shore * smoothstep(0.6, 1.0, wave) * smoothstep(0.35, 0.65, nf.g);
	foam += (1.0 - smoothstep(0.0, 0.02, abs(wv - 0.515))) * 0.55;
	water = lerp(water, float3(0.9, 0.94, 0.93), saturate(foam) * 0.75);

	// Fischschwarm: vier Fische ziehen unter der Oberfläche ihre Bahnen, mit
	// Schatten auf dem Grund (im Flachen deutlich, im Tiefen kaum) und ab und zu
	// einem Ring, wo einer nach einer Fliege schnappt. Jeder Schwarm hat seinen
	// eigenen Takt aus dem Rauschen an seiner Kachel
	float school = smoothstep(0.35, 0.65, live.b);
	if (school > 0.0)
	{
		float2 cell = floor(tile);
		float2 f = tile - cell;
		float seed = tex2Dlod(NoiseSampler, float4(cell * 0.137, 0.0, 0.0)).a * 6.283;
		float2 shadowOffset = float2(0.03, 0.045) * (1.0 + depth * 1.5);   // Licht von links oben
		float fish = 0.0;
		float shadow = 0.0;
		for (int k = 0; k < 4; k++)
		{
			float2 dir;
			float2 pos = FishPath(Time, seed, k, dir);
			float beat = sin(Time * (7.0 + k) + seed + k * 1.9);
			fish = max(fish, FishMask(f - pos, dir, beat));
			shadow = max(shadow, FishMask(f - pos - shadowOffset, dir, beat));
		}
		water = lerp(water, water * 0.72, shadow * school * (1.0 - depth * 0.7) * 0.6);
		water = lerp(water, water * float3(0.38, 0.4, 0.36) + float3(0.03, 0.03, 0.01), fish * school * 0.85);
		water = lerp(water, float3(0.85, 0.92, 0.95), FishRing(f, Time, seed) * school * 0.6);
	}

	return float4(lerp(ground, water, inWater), 1.0);
}

technique Boden
{
	pass P0
	{
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
};
