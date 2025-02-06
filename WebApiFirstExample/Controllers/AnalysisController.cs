using DocumentFormat.OpenXml.Packaging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Text;
using UglyToad.PdfPig;
using WebApiFirstExample.Data;
using WebApiFirstExample.Model;

namespace WebApiFirstExample.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AnalysisController : ControllerBase
    {
        private readonly ILLMService _llmService;
        private readonly ILogger<AnalysisController> _logger;
        private readonly ApplicationDBContext _context;

        public AnalysisController(ILLMService llmService, ILogger<AnalysisController> logger, ApplicationDBContext context)
        {
            _llmService = llmService;
            _logger = logger;
            _context = context;
        }

        [HttpPost("analyze-resume")]
        public async Task<IActionResult> UploadResume([FromForm] IFormFile file)
        {
            try
            {
                //if (string.IsNullOrWhiteSpace(request?.Text))
                //{
                //    return BadRequest(new { Error = "Texto de currículum requerido" });
                //}

                if (file == null || file.Length == 0) return BadRequest("No file uploaded.");

                // Guardar el archivo en una ubicación temporal
                var tempFilePath = Path.GetTempFileName();
                using (var stream = new FileStream(tempFilePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Determinar el tipo de archivo y extraer el texto
                string extractedText = ExtractTextFromFile(tempFilePath, file.FileName);

                // Eliminar el archivo temporal
                System.IO.File.Delete(tempFilePath);

                // Enviar texto a LM Studio (Phi-2) para análisis

                var analysis = await _llmService.AnalyzeResumeAsync(extractedText);
                return Ok(new { Analysis = analysis });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en AnalyzeResume");
                return StatusCode(500, new { Error = "Error interno del servidor" });
            }
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ResumeAnalysis>>> GetAllAnalyses()
        {
            var analyses = await _context.ResumeAnalyses.ToListAsync();
            return Ok(analyses);
        }

        private string ExtractTextFromFile(string filePath, string fileName)
        {
            string extension = Path.GetExtension(fileName).ToLower();

            if (extension == ".pdf")
            {
                return ExtractTextFromPdf(filePath);
            }
            else if (extension == ".docx")
            {
                return ExtractTextFromDocx(filePath);
            }
            else
            {
                throw new NotSupportedException("Unsupported file format. Please upload a PDF or DOCX file.");
            }
        }

        private string ExtractTextFromPdf(string filePath)
        {
            StringBuilder text = new StringBuilder();
            using (PdfDocument document = PdfDocument.Open(filePath))
            {
                foreach (var page in document.GetPages())
                {
                    text.Append(page.Text);
                    text.Append("\n");
                }
            }
            return text.ToString();
        }

        private string ExtractTextFromDocx(string filePath)
        {
            StringBuilder text = new StringBuilder();
            using (WordprocessingDocument doc = WordprocessingDocument.Open(filePath, false))
            {
                foreach (var paragraph in doc.MainDocumentPart.Document.Body.Elements<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
                {
                    text.Append(paragraph.InnerText);
                    text.Append("\n");
                }
            }
            return text.ToString();
        }
    }

    public class ResumeAnalysisRequest
    {
        [Required]
        public string Text { get; set; }
    }
}
