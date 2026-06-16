using Microsoft.AspNetCore.Mvc; 
using Microsoft.EntityFrameworkCore; 
using CryptoWallet.Data; 
using CryptoWallet.Models; 

namespace CryptoWallet.Controllers 
{
    [ApiController] 
    [Route("transactions")] 
    public class TransactionsController : ControllerBase 
    {
        private readonly AppDbContext _context; 
        private readonly HttpClient _httpClient; 

        public TransactionsController(AppDbContext context, IHttpClientFactory httpClientFactory) 
        {
            _context = context; 
            _httpClient = httpClientFactory.CreateClient(); 
        }

        [HttpGet]
        public async Task<IActionResult> GetAll() 
        {
            var transactions = await _context.Transactions 
                .OrderByDescending(t => t.DateTime) 
                .ToListAsync(); 
            return Ok(transactions); 
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id) 
        {
            var transaction = await _context.Transactions.FindAsync(id); 
            if (transaction == null) return NotFound(); 
            return Ok(transaction); 
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] TransactionRequest request) 
        {
            if (request.CryptoAmount <= 0)
                return BadRequest("La cantidad de criptomoneda debe ser mayor a 0."); 

            if (request.Action != "purchase" && request.Action != "sale") 
                return BadRequest("La acción debe ser 'purchase' o 'sale'."); 

            var cryptosValidas = new[] { "btc", "eth", "usdc" }; 
            if (!cryptosValidas.Contains(request.CryptoCode.ToLower()))
                return BadRequest("Criptomoneda no válida. Usar: btc, eth, usdc."); 

            
            if (request.Action == "sale") 
            {
                var saldo = await GetSaldo(request.CryptoCode); 
                if (saldo < request.CryptoAmount) 
                    return BadRequest($"No tenés suficiente {request.CryptoCode}. Saldo actual: {saldo}");
            }

            decimal precio = await GetPrecioCripto(request.CryptoCode); 
            decimal money = precio * request.CryptoAmount; 

            var transaction = new Transaction 
            {
                CryptoCode = request.CryptoCode.ToLower(), 
                Action = request.Action, 
                CryptoAmount = request.CryptoAmount, 
                Money = money, 
                DateTime = request.DateTime 
            };

            _context.Transactions.Add(transaction); 
            await _context.SaveChangesAsync(); 

            return Ok(transaction); 
        }

        [HttpPatch("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] TransactionUpdateRequest request) 
        {
            var transaction = await _context.Transactions.FindAsync(id); 
            if (transaction == null) return NotFound(); 

            if (request.CryptoCode != null) transaction.CryptoCode = request.CryptoCode; 
            if (request.Action != null) transaction.Action = request.Action; 
            if (request.CryptoAmount.HasValue) transaction.CryptoAmount = request.CryptoAmount.Value; 
            if (request.Money.HasValue) transaction.Money = request.Money.Value; 
            if (request.DateTime.HasValue) transaction.DateTime = request.DateTime.Value; 

            await _context.SaveChangesAsync(); 
            return Ok(transaction); 
        }

        [HttpDelete("{id}")] 
        public async Task<IActionResult> Delete(int id) 
        {
            var transaction = await _context.Transactions.FindAsync(id); 
            if (transaction == null) return NotFound(); 

            _context.Transactions.Remove(transaction); 
            await _context.SaveChangesAsync(); 
            return Ok(); 
        }

        [HttpGet("analysis")] 
        public async Task<IActionResult> GetAnalysis() 
        {
            var transactions = await _context.Transactions.ToListAsync(); 
            var cryptos = new[] { "btc", "eth", "usdc" }; 
            var resultado = new List<object>(); 
            decimal totalGeneral = 0;

            foreach (var crypto in cryptos) 
            {
                var saldo = transactions 
                    .Where(t => t.CryptoCode == crypto && t.Action == "purchase") 
                    .Sum(t => t.CryptoAmount) 
                    - transactions 
                    .Where(t => t.CryptoCode == crypto && t.Action == "sale") 
                    .Sum(t => t.CryptoAmount); 

                if (saldo > 0) 
                {
                    decimal precio = await GetPrecioCripto(crypto); 
                    decimal valorTotal = saldo * precio;
                    totalGeneral += valorTotal; 

                    resultado.Add(new 
                    {
                        crypto_code = crypto, 
                        cantidad = saldo, 
                        dinero = valorTotal 
                    });
                }
            }

            return Ok(new { cartera = resultado, total = totalGeneral }); 
        }

        private async Task<decimal> GetSaldo(string cryptoCode) 
        {
            var compras = await _context.Transactions 
                .Where(t => t.CryptoCode == cryptoCode && t.Action == "purchase") 
                .ToListAsync(); 

            var ventas = await _context.Transactions 
                .Where(t => t.CryptoCode == cryptoCode && t.Action == "sale") 
                .ToListAsync(); 

            return compras.Sum(t => t.CryptoAmount) - ventas.Sum(t => t.CryptoAmount); 
        }

        private async Task<decimal> GetPrecioCripto(string cryptoCode) 
        {
            try 
            {
                var url = $"https://criptoya.com/api/satoshitango/{cryptoCode}/ars/1";
                var response = await _httpClient.GetFromJsonAsync<CriptoYaResponse>(url); 
                return response?.TotalBid ?? 0; 
            }
            catch 
            {
                return 0;
            }
        }
    }

    public class TransactionRequest 
    {
        public string CryptoCode { get; set; } = string.Empty; 
        public string Action { get; set; } = string.Empty;  
        public decimal CryptoAmount { get; set; } 
        public DateTime DateTime { get; set; } 
    }

    public class TransactionUpdateRequest {
        public string? CryptoCode { get; set; } 
        public string? Action { get; set; } 
        public decimal? CryptoAmount { get; set; } 
        public decimal? Money { get; set; } 
        public DateTime? DateTime { get; set; } 
    }

    public class CriptoYaResponse 
    {
        public decimal Ask { get; set; } 
        public decimal TotalAsk { get; set; } 
        public decimal Bid { get; set; } 
        public decimal TotalBid { get; set; }
    }
}