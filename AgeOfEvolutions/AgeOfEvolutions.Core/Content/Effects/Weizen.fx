// Weizen im Wind: reife Felder (Felder/weizen) wiegen sich, und Böen laufen als
// helle Bänder über das Feld.
//
// RTSGameplayScreen.DrawWheat zeichnet jede Feldkachel einzeln (SpriteSortMode.
// Immediate) mit ihrem Teil des Feldbilds (FieldPart) und setzt davor PartRect und
// TileOrigin. Die Texturkoordinate läuft über das ganze Feldbild von 0 bis 1 - am
// Rand steht der Zaun, der bewegt sich nicht.

#if OPENGL
	#define SV_POSITION POSITION
	#define PS_SHADERMODEL ps_3_0
#else
	#define PS_SHADERMODEL ps_4_0
#endif

float Time;          // Sekunden, Takt des Windes
float4 PartRect;     // Teil des Feldbilds dieser Kachel: x, y, Breite, Höhe in Texturkoordinaten
float2 TileOrigin;   // linke obere Ecke dieser Kachel auf der Karte, in Kacheln

Texture2D SpriteTexture;   // das Feldbild, setzt der SpriteBatch
sampler2D WheatSampler = sampler_state
{
	Texture = <SpriteTexture>;
	AddressU = Clamp;
	AddressV = Clamp;
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

// Der Wind an der Stelle world (Kachelkoordinaten auf der Karte) zur Zeit t (Sekunden).
// Rückgabe: xy wie weit die Ähren dort ausgelenkt sind, in Kacheln; z ihre Helligkeit
// (1 = wie im Bild).
// VERTRAG:
// - Der Wind kommt von links und etwas von oben: Windrichtung normalize(float2(1.0, 0.35)).
// - Böen sind helle Bänder quer zur Windrichtung, die mit 1,2 Kacheln je Sekunde in
//   Windrichtung über die Felder laufen, im Abstand von etwa 4 bis 6 Kacheln. Nicht
//   streng regelmäßig: zwei Wellen mit verschiedener Wellenlänge und leicht
//   verschiedener Richtung überlagern, dazu ein langsames An- und Abschwellen der
//   Stärke (etwa 0.7 + 0.3 * sin(0.4 * t)).
// - z ist 1,0 ohne Böe; mitten in einer Böe bis 1,25 (die Halme biegen sich und
//   zeigen ihre hellere Seite); direkt hinter einer Böe etwas dunkler, bis 0,92.
// - xy: die Ähren neigen sich in Windrichtung, so weit wie die Böe dort stark ist,
//   höchstens 0,05 Kacheln; dazu ein leichtes Zittern von höchstens 0,008 Kacheln,
//   schnell (etwa fünfmal je Sekunde) und von Stelle zu Stelle versetzt, etwa aus
//   sin(world.x * 9.0 + world.y * 4.0 + t * 5.0).
// - Stetig in world und t: keine Sprünge, kein frac oder floor auf world.
// - Keine Texturzugriffe, keine Schleifen.
float3 WheatWind(float2 world, float t)
{
	// Zwei Böenwellen: verschiedene Wellenlänge, leicht gedrehte Richtung
	float2 d1 = normalize(float2(1.0, 0.2));
	float2 d2 = normalize(float2(1.0, 0.5));
	float w1 = sin((dot(world, d1) - 1.2 * t) * 6.2832 / 4.0);
	float w2 = sin((dot(world, d2) - 1.2 * t) * 6.2832 / 6.0);
	// Bänder statt Sinuswellen: schärfen
	float b1 = pow(saturate(w1), 3.0);
	float b2 = pow(saturate(w2), 3.0);
	float band = max(b1, b2);
	// Langsames An- und Abschwellen der Stärke
	float strength = 0.7 + 0.3 * sin(0.4 * t);
	float gust = band * strength;
	// Direkt hinter einer Böe etwas dunkler: dieselbe Welle versetzt (Phase + 1.0)
	float w1b = sin((dot(world, d1) - 1.2 * t) * 6.2832 / 4.0 + 1.0);
	float w2b = sin((dot(world, d2) - 1.2 * t) * 6.2832 / 6.0 + 1.0);
	float behind = max(pow(saturate(w1b), 3.0), pow(saturate(w2b), 3.0)) * strength;
	// Helligkeit: 1.0 ohne Böe, bis 1.25 mitten in einer, bis 0.92 direkt dahinter
	float brightness = 1.0 + 0.25 * gust - 0.08 * behind;
	// Neigung in Windrichtung, höchstens 0.05 Kacheln
	float2 dir = normalize(float2(1.0, 0.35));
	float2 lean = dir * 0.05 * gust;
	// Leichtes Zittern, höchstens 0.008 Kacheln, schnell und von Stelle zu Stelle versetzt
	float jitter = sin(world.x * 9.0 + world.y * 4.0 + t * 5.0);
	float2 jitterVec = dir * 0.008 * jitter;
	return float3(lean.x + jitterVec.x, lean.y + jitterVec.y, brightness);
}

float4 MainPS(VertexShaderOutput input) : COLOR
{
	float2 uv = input.TextureCoordinates;
	// Lage auf der Karte, in Kacheln
	float2 world = TileOrigin + (uv - PartRect.xy) / PartRect.zw;
	float3 wind = WheatWind(world, Time);
	// Der Zaun am Feldrand steht still: nur innen auslenken und aufhellen
	float edge = min(min(uv.x, 1.0 - uv.x), min(uv.y, 1.0 - uv.y));
	float inner = smoothstep(0.07, 0.12, edge);
	// Eine Kachel ist im Feldbild PartRect.zw groß. Gegen die Neigung abgetastet,
	// damit die Ähren in Windrichtung wandern
	float4 color = tex2D(WheatSampler, uv - wind.xy * PartRect.zw * inner) * input.Color;
	// Vormultipliziertes Alpha: heller höchstens bis zur Deckkraft
	color.rgb = min(color.rgb * lerp(1.0, wind.z, inner), color.a);
	return color;
}

technique Weizen
{
	pass P0
	{
		PixelShader = compile PS_SHADERMODEL MainPS();
	}
};
