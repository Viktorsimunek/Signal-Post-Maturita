using System;
using System.IO;
using System.IO.Compression;

// Ciste C#: muze bezet na pracovnim vlakne bez volani Unity API.
public static class FaxPalettePng
{
    private static readonly int[] Bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
    private static readonly uint[] CrcTable = CreateCrcTable();

    public static byte[] Encode(byte[] rgba, int width, int height)
    {
        if (width <= 0 || height <= 0 || rgba.Length != checked(width * height * 4))
            throw new ArgumentException("Neplatna velikost RGBA faxu.");
        int stride = (width + 3) / 4;
        byte[] scanlines = new byte[checked((stride + 1) * height)];
        for (int y = 0; y < height; y++)
        {
            // Unity readback ma prvni radek dole; PNG ma prvni radek nahore.
            int sourceRow = (height - 1 - y) * width * 4;
            int row = y * (stride + 1) + 1; // Prvni byte = PNG filter None.
            for (int x = 0; x < width; x++)
            {
                int p = sourceRow + x * 4;
                float gray = (rgba[p] * 0.299f + rgba[p + 1] * 0.587f + rgba[p + 2] * 0.114f) / 255f;
                float dither = (Bayer[(y & 3) * 4 + (x & 3)] + 0.5f) / 16f - 0.5f;
                int level = Math.Max(0, Math.Min(3, (int)Math.Floor(gray * 3f + dither + 0.5f)));
                scanlines[row + x / 4] |= (byte)(level << (6 - (x & 3) * 2));
            }
        }

        byte[] compressed;
        using (var stream = new MemoryStream())
        {
            // PNG pouziva zlib: hlavicka + raw DEFLATE + Adler32.
            stream.WriteByte(0x78);
            stream.WriteByte(0x9c);
            using (var deflate = new DeflateStream(stream, CompressionLevel.Optimal, true))
                deflate.Write(scanlines, 0, scanlines.Length);
            uint a = 1, b = 0;
            foreach (byte value in scanlines) { a = (a + value) % 65521; b = (b + a) % 65521; }
            WriteUInt(stream, (b << 16) | a);
            compressed = stream.ToArray();
        }

        using (var png = new MemoryStream())
        {
            png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, 0, 8);
            byte[] header;
            using (var h = new MemoryStream())
            {
                WriteUInt(h, (uint)width); WriteUInt(h, (uint)height);
                h.Write(new byte[] { 2, 3, 0, 0, 0 }, 0, 5); // 2 bity, paleta, bez alpha.
                header = h.ToArray();
            }
            Chunk(png, "IHDR", header);
            Chunk(png, "PLTE", new byte[] { 0, 0, 0, 85, 85, 85, 170, 170, 170, 255, 255, 255 });
            Chunk(png, "IDAT", compressed);
            Chunk(png, "IEND", Array.Empty<byte>());
            return png.ToArray();
        }
    }

    private static void Chunk(Stream stream, string name, byte[] data)
    {
        byte[] type = System.Text.Encoding.ASCII.GetBytes(name);
        WriteUInt(stream, (uint)data.Length);
        stream.Write(type, 0, 4); stream.Write(data, 0, data.Length);
        uint crc = 0xffffffff;
        foreach (byte value in type) crc = CrcTable[(crc ^ value) & 255] ^ (crc >> 8);
        foreach (byte value in data) crc = CrcTable[(crc ^ value) & 255] ^ (crc >> 8);
        WriteUInt(stream, crc ^ 0xffffffff);
    }

    private static void WriteUInt(Stream stream, uint value)
    {
        stream.WriteByte((byte)(value >> 24)); stream.WriteByte((byte)(value >> 16));
        stream.WriteByte((byte)(value >> 8)); stream.WriteByte((byte)value);
    }

    private static uint[] CreateCrcTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int j = 0; j < 8; j++) c = (c & 1) != 0 ? 0xedb88320 ^ (c >> 1) : c >> 1;
            table[i] = c;
        }
        return table;
    }
}
