// rotates the 2D vector v by angle a (in radians) around the origin (0, 0)
float2 Rotate2D(float2 v, float a)
{
    float s = sin(a);
    float c = cos(a);
    return float2(v.x * c - v.y * s, v.x * s + v.y * c);
}

float _UnscaledTime;

float QuantizedTime(float fps)
{
    float safeFps = max(fps, 1.0);
    return floor(_UnscaledTime * safeFps) / safeFps;
}

float Hash21(float2 p)
{
    p = frac(p * float2(0.1031, 0.1030));
    p += dot(p, p.yx + 33.33);
    return frac((p.x + p.y) * p.x);
}

float2 Hash22(float2 p)
{
    float n1 = Hash21(p + float2(17.0, 43.0));
    float n2 = Hash21(p + float2(71.0, 29.0));
    return float2(n1, n2) * 2.0 - 1.0;
}

float ValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);

    float a = Hash21(i + float2(0.0, 0.0));
    float b = Hash21(i + float2(1.0, 0.0));
    float c = Hash21(i + float2(0.0, 1.0));
    float d = Hash21(i + float2(1.0, 1.0));

    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

float Fractal2(float2 p)
{
    float n = ValueNoise(p);
    n += ValueNoise(p * 2.07 + float2(19.1, 7.3)) * 0.5;
    return (n / 1.5) * 2.0 - 1.0;
}
