namespace FastLabel.Backend.Services
{
    public static class LabelRendererService
    {
        // Cache statique pour éviter de recharger les fichiers .ttf en mémoire à chaque rendu
        private static readonly Dictionary<string, SKTypeface?> _typefaces = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Lock _lock = new();

        private static SKTypeface GetTypeface(string fontFamily, bool isBold, bool isItalic, float fontSize)
        {
            string cleanName = fontFamily.Split(',')[0].Trim().Trim('\'', '"');

            // 1. Détermination du suffixe de style
            string styleSuffix = (isBold, isItalic) switch
            {
                (true, true) => "BoldItalic",
                (true, false) => "Bold",
                (false, true) => "Italic",
                (false, false) => "Regular"
            };

            // 2. Détermination directe du nom de fichier unique selon la police
            string fileName = cleanName switch
            {
                "Inter" => $"Inter_{(fontSize <= 21 ? 18 : fontSize <= 26 ? 24 : 28)}pt-{styleSuffix}.ttf",
                "Lora" => $"Lora-{styleSuffix}.ttf",
                "JetBrains Mono" => $"JetBrainsMono-{styleSuffix}.ttf",
                "Share Tech Mono" => "ShareTechMono-Regular.ttf", // N'a qu'une seule variante
                _ => "Inter_24pt-Regular.ttf"
            };

            // Le nom du fichier fait office de clé de cache naturelle, c'est du 2 en 1
            string cacheKey = fileName;

            lock (_lock)
            {
                if (_typefaces.TryGetValue(cacheKey, out var cachedTypeface) && cachedTypeface != null)
                {
                    return cachedTypeface;
                }

                string fontPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fonts", fileName);

                SKTypeface? typeface = null;
                if (File.Exists(fontPath))
                {
                    typeface = SKTypeface.FromFile(fontPath);
                }

                // Repli de secours si le fichier précis manque
                if (typeface == null)
                {
                    string fallbackPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fonts", "Inter_24pt-Regular.ttf");
                    if (File.Exists(fallbackPath))
                    {
                        typeface = SKTypeface.FromFile(fallbackPath);
                    }
                }

                _typefaces[cacheKey] = typeface ?? SKTypeface.Default;
                return _typefaces[cacheKey]!;
            }
        }

        public static string RenderLabelToPngBase64(string fabricJson, Dictionary<string, string> variables)
        {
            using var doc = JsonDocument.Parse(fabricJson);
            var root = doc.RootElement;

            int width = root.TryGetProperty("canvasWidth", out var wProp) ? wProp.GetInt32() : 500;
            int height = root.TryGetProperty("canvasHeight", out var hProp) ? hProp.GetInt32() : 300;

            using var surface = SKSurface.Create(new SKImageInfo(width, height));
            var canvas = surface.Canvas;
            
            canvas.Clear(SKColors.White);

            if (root.TryGetProperty("objects", out var objectsProp) && objectsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var obj in objectsProp.EnumerateArray())
                {
                    string type = obj.GetProperty("type").GetString() ?? "";
                    float left = obj.GetProperty("left").GetSingle();
                    float top = obj.GetProperty("top").GetSingle();

                    if (type == "i-text" || type == "text")
                    {
                        string text = obj.GetProperty("text").GetString() ?? "";
                        
                        foreach (var v in variables)
                        {
                            text = text.Replace($"{{{{{v.Key}}}}}", v.Value);
                        }

                        float fontSize = obj.TryGetProperty("fontSize", out var fs) ? fs.GetSingle() : 16;
                        string fontFamily = obj.TryGetProperty("fontFamily", out var ff) ? ff.GetString() ?? "Inter" : "Inter";

                        // Extraction sécurisée des styles Gras / Italique
                        string fontWeight = obj.TryGetProperty("fontWeight", out var fw) ? (fw.ValueKind == JsonValueKind.String ? fw.GetString() ?? "normal" : fw.GetRawText()) : "normal";
                        string fontStyle = obj.TryGetProperty("fontStyle", out var fst) ? fst.GetString() ?? "normal" : "normal";

                        bool isBold = fontWeight == "bold" || (int.TryParse(fontWeight, out int wNum) && wNum >= 600);
                        bool isItalic = fontStyle == "italic";

                        // On passe bien fontSize à la méthode pour gérer les tailles optiques d'Inter
                        var typeface = GetTypeface(fontFamily, isBold, isItalic, fontSize);
                        using var font = new SKFont(typeface, fontSize);

                        using var paint = new SKPaint
                        {
                            Color = SKColors.Black,
                            IsAntialias = false 
                        };

                        canvas.DrawText(text, left, top + fontSize, SKTextAlign.Left, font, paint);
                    }
                    else if (type == "rect")
                    {
                        float w = obj.GetProperty("width").GetSingle() * obj.GetProperty("scaleX").GetSingle();
                        float h = obj.GetProperty("height").GetSingle() * obj.GetProperty("scaleY").GetSingle();
                        float rx = obj.TryGetProperty("rx", out var rxProp) ? rxProp.GetSingle() : 0;
                        float ry = obj.TryGetProperty("ry", out var ryProp) ? ryProp.GetSingle() : 0;

                        using var paint = new SKPaint
                        {
                            Color = SKColor.Parse(obj.TryGetProperty("fill", out var fill) ? fill.GetString() ?? "#000000" : "#000000"),
                            IsAntialias = false, 
                            Style = SKPaintStyle.Fill
                        };

                        var rect = new SKRect(left, top, left + w, top + h);
                        if (rx > 0 || ry > 0)
                        {
                            canvas.DrawRoundRect(rect, rx, ry, paint);
                        }
                        else
                        {
                            canvas.DrawRect(rect, paint);
                        }
                    }
                }
            }

            canvas.Flush();
            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            
            return Convert.ToBase64String(data.ToArray());
        }
    }
}