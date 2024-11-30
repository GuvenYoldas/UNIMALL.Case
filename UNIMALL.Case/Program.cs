using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RestSharp;

class Program
{
    // OpenAI API anahtar tanımım
    public static string openAiApiKey = "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";
    static async Task Main(string[] args)
    {
        // Ekrana basacağım objem.
        ProductList extractedData = new ProductList();
        extractedData.itemListElement = new List<ProductItem>();

        // Trendyol datalarını aldığım method.
        var trendyolData = await TrendyolMethod();
        extractedData.itemListElement.AddRange(trendyolData.itemListElement);

        //OzonRu datalarını aldığım method.
        var ozonRuData = await OzonRuMethod();
        extractedData.itemListElement.AddRange(ozonRuData.itemListElement);


        // Çıktıyı yazdırıyorum.
        foreach (var item in extractedData.itemListElement)
        {
            Console.WriteLine($"Ürün Adı: {item.name}");
            Console.WriteLine($"Fiyat: {item.price}");
            Console.WriteLine($"Resim URL'si: {item.imageUrl}");

            Console.WriteLine($"");
            Console.WriteLine($"");
            Console.WriteLine($"");
        }
    }

    //  Siteden datayı çektiğim method
    static async Task<string> FetchHtmlContent(string url)
    {
        using HttpClient client = new HttpClient();
        return await client.GetStringAsync(url);
    }

    // AI'a komut verdiğim method
    static string CreatePrompt(string htmlContent)
    {
        // AI'a yapması gerekeni söylüyorum.
        return $@"
JSON içeriği aşağıda verilmiştir. 
Bu JSON'den ürünlerin ürün adı, fiyat ve resim URL bilgisini çıkar ve JSON formatında liste halinde döndür. 
Sadece JSON formatında yanıt ver. Ürün adı alanına yazacağın değer hangi dilde olursa olsun bunu İngilizce diline çevirerek yaz.

HTML:
{htmlContent}

Cevap formatı:
{{ ""itemListElement"": [
    {{
    ""name"": ""Ürün Adı"",
    ""price"": ""Fiyat"",
    ""imageUrl"": ""Resim URL""
}}
]
}}
cevap formatındaki tüm alanlar string olmalı.";
    }
    // AI a burada request atıp response'unu alıyorum.
    static async Task<ProductList> AnalyzeHtmlWithGPT(string prompt, string apiKey)
    {
        var client = new RestClient("https://api.openai.com/v1/chat/completions");
        var request = new RestRequest("", Method.Post);
        request.AddHeader("Authorization", $"Bearer {apiKey}");
        request.AddHeader("Content-Type", "application/json");

        var body = new
        {
            model = "gpt-3.5-turbo", // GPT modeli olarak ucuz olduğu için bunu seçiyorum. (gpt-4 de olabilirdi model, diğer modellerin listesi var).
            messages = new[]
            {
                    new { role = "system", content = "You are an assistant that extracts key data from HTML." },
                    new { role = "user", content = prompt }
                },
            temperature = 0.2
        };

        request.AddJsonBody(body);
        var response = await client.ExecuteAsync(request);

        if (response.IsSuccessful)
        {
            // JSON'u parse ediyorum
            var jsonObject = JObject.Parse(response.Content);

            // Dinamik olarak content alanını alıyorum.
            string content = jsonObject["choices"]?[0]?["message"]?["content"]?.ToString();

            // Başında ve sonunda deserialize olacak stringleri temizliyorum. bunun bir fonksiyonu da vardır muhtemelen.
            content = content.Replace("```json", "").Replace("```", "");

            // Deserialize ediyorum.
            return JsonSerializer.Deserialize<ProductList>(content);
        }

        throw new Exception($"OpenAI API isteği başarısız: {response.Content}");
    }


    static async Task<ProductList> TrendyolMethod()
    {
        // URL 'i giriyorum. 
        string url = "https://www.trendyol.com/asus-monitor-x-b101606-c103668";

        // HTML içeriğini alıyorum.
        string htmlContent = await FetchHtmlContent(url);

        // Token masrafı olmasın diye içerideki item listesini alacak şekilde string'i düzenliyorum.
        string indexline = "href='https://www.trendyol.com/ar/asus-monitor-x-b101606-c103668'><script type=\"application/ld+json\">{\"@context\": \"https://schema.org\",\"@type\": \"ItemList\",";
        int startIndex = htmlContent.IndexOf(indexline) + indexline.Count(); // "[" sonrasını al.
        int endIndex = htmlContent.IndexOf(" }, </script>"); // "]" öncesini al.
        string htmlContentResult = "{ " + htmlContent.Substring(startIndex, endIndex - startIndex) + " }";

        // JSON olarak kalan datayı OpenAI GPT ile analiz ediyorum
        string prompt = CreatePrompt(htmlContentResult);
        return await AnalyzeHtmlWithGPT(prompt, openAiApiKey);
    }

    static async Task<ProductList> OzonRuMethod()
    {
        // URL 'i giriyorum. 
        string url = "https://www.ozon.ru/category/noutbuki-planshety-i-elektronnye-knigi-8730/?brand=26303007%2C86812567&brand_was_predicted=true&currency_price=70000.000%3B83590.000&delivery=1&deny_category_prediction=true&from_global=true&is_high_rating=t&ramlaptopsandpcforfilters=100209288&seller=0&technologymatrixnote=272812&text=ASUS";

        // HTML içeriğini alıyorum
        string htmlContent = await FetchHtmlContent(url);

        // Token masrafı olmasın diye içerideki item listesini alacak şekilde string'i düzenliyorum.
        string indexline = "<div id=\"state-searchResultsV2-3547909-default-1\" data-state='{\"tileLayout\":\"LAYOUT_GRID4\",";
        int startIndex = htmlContent.IndexOf(indexline) + indexline.Count(); // "[" sonrasını al.
        int endIndex = htmlContent.IndexOf(",\"templates\""); // "]" öncesini al.
        string htmlContentResult = "{ " + htmlContent.Substring(startIndex, endIndex - startIndex) + " }";

        // JSON olarak kalan datayı OpenAI GPT ile analiz ediyorum
        string prompt = CreatePrompt(htmlContentResult);
        return await AnalyzeHtmlWithGPT(prompt, openAiApiKey);
    }
}

// AI'ın döndüğü json formatını parse edeceğim objeler. 
class ProductItem
{
    public string name { get; set; }
    public string price { get; set; }
    public string imageUrl { get; set; }
}

class ProductList
{
    public List<ProductItem> itemListElement { get; set; }
}