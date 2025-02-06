using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Logging;
using System.Text;
using org.apache.pdfbox.tools;
using WebApiFirstExample.Model;
using WebApiFirstExample.Data;
using Microsoft.AspNetCore.Mvc;

public interface ILLMService
{
    Task<string> AnalyzeResumeAsync(string text);
}

public class LLMService : ILLMService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LLMService> _logger;
    private readonly string _modelName;
    private readonly string _apiUrl;
    private readonly ApplicationDBContext _context;

    public LLMService(HttpClient httpClient, IConfiguration config, ILogger<LLMService> logger, ApplicationDBContext context)
    {
        _context = context;
        _httpClient = httpClient;
        _logger = logger;
        _modelName = config["LLMConfig:Model"] ?? "Mistral-7B-Instruct-v0.1";
        _apiUrl = config["LLMConfig:ApiUrl"] ?? "http://localhost:1234/v1/chat/completions";
    }

    public async Task<string> AnalyzeResumeAsync(string text)
    {
        try
        {
            var payload = new
            {
                model = _modelName,
                messages = new[]
                {
                    new { role = "system", content = "Eres un experto en análisis de currículums. Proporciona feedback detallado y mejoras estructuradas." },
                    new { role = "user", content = $"Analiza este currículum y brinda recomendaciones: {text}" }
                },
                temperature = 0.7,
                max_tokens = 3500,
                top_p = 0.9
            };

            var jsonPayload = JsonConvert.SerializeObject(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(_apiUrl, content);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"Error en LLM - Código: {response.StatusCode}, Respuesta: {responseString}");
                return "Error analizando el currículum. Por favor intenta nuevamente.";
            }

            var jsonResponse = JObject.Parse(responseString);
            
            //Guardar en la base de datos
            string? v = jsonResponse["choices"]?[0]?["message"]?["content"]?.ToString();
            var analysis = new ResumeAnalysis
            {
                FileName = "Example Name",
                ExtractedText = text,
                LLMAnalysis = v,
            };

            _context.ResumeAnalyses.Add(analysis);
            await _context.SaveChangesAsync();

            return jsonResponse["choices"]?[0]?["message"]?["content"]?.ToString()
            ?? "No se pudo interpretar la respuesta del modelo";

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en AnalyzeResumeAsync");
            return "Error interno procesando tu solicitud";
        }
    }
}