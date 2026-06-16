using Microsoft.AspNetCore.Mvc; //Importa lo necesario para usar controladores.
using Microsoft.EntityFrameworkCore; //Importa Entity Framework Core para usar métodos como ToListAsync().
using CryptoWallet.Data; //Importa AppDbContext
using CryptoWallet.Models; //Importa Transaction

namespace CryptoWallet.Controllers //Define el espacio de nombres.
{
    [ApiController] //Le dice a ASP.NET que esta clase es un controlador de API
    [Route("transactions")] //Todos los endpoints de esta clase empiezan con /transactions
    public class TransactionsController : ControllerBase //Define la clase. Hereda de ControllerBase
    {
        private readonly AppDbContext _context; //Declara la variable para la base de datos
        private readonly HttpClient _httpClient; //Declara la variable para llamar a APIs externas

        public TransactionsController(AppDbContext context, IHttpClientFactory httpClientFactory) //El constructor recibe los dos servicios por inyección de dependencias
        {
            _context = context; //Guarda el contexto en la variable privada
            _httpClient = httpClientFactory.CreateClient(); //Crea el HttpClient y lo guarda
        }

        [HttpGet]
        public async Task<IActionResult> GetAll() //Devuelve todas las transacciones.
        {
            var transactions = await _context.Transactions //Consulta la tabla Transactions
                .OrderByDescending(t => t.DateTime) //Las ordena de más nueva a más vieja
                .ToListAsync(); //Ejecuta la consulta y devuelve una lista.
            return Ok(transactions); //Devuelve las transacciones con código HTTP 200
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id) //Busca una transacción por ID
        {
            var transaction = await _context.Transactions.FindAsync(id); //Busca en la base de datos por ID
            if (transaction == null) return NotFound(); //Si no existe devuelve que no exite con codigo 404
            return Ok(transaction); //Si existe la devuelve con 200 
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] TransactionRequest request) //Recibe los datos del body como TransactionRequest
        {
            if (request.CryptoAmount <= 0)
                return BadRequest("La cantidad de criptomoneda debe ser mayor a 0."); //Valida que la cantidad sea positiva

            if (request.Action != "purchase" && request.Action != "sale") //Valida que la acción sea una de las dos permitidas.
                return BadRequest("La acción debe ser 'purchase' o 'sale'."); 

            var cryptosValidas = new[] { "btc", "eth", "usdc" }; //Valida que la cripto sea una de las tres permitidas
            if (!cryptosValidas.Contains(request.CryptoCode.ToLower()))
                return BadRequest("Criptomoneda no válida. Usar: btc, eth, usdc."); 

            
            if (request.Action == "sale") //Si es una venta entra a este bloque
            {
                var saldo = await GetSaldo(request.CryptoCode); //Calcula el saldo actual de esa cripto
                if (saldo < request.CryptoAmount) //Si no tenés suficiente saldo devuelve error.
                    return BadRequest($"No tenés suficiente {request.CryptoCode}. Saldo actual: {saldo}");
            }

            decimal precio = await GetPrecioCripto(request.CryptoCode); //Obtiene el precio actual de la cripto desde CriptoYa
            decimal money = precio * request.CryptoAmount; //Calcula el monto en pesos.

            var transaction = new Transaction //Crea el objeto Transaction.
            {
                CryptoCode = request.CryptoCode.ToLower(), //Guarda el código en minúsculas
                Action = request.Action, //Guarda la acción. 
                CryptoAmount = request.CryptoAmount, //Guarda la cantidad.
                Money = money, //Guarda el monto calculado.
                DateTime = request.DateTime //Guarda la fecha y hora.
            };

            _context.Transactions.Add(transaction); //Agrega la transacción al contexto.
            await _context.SaveChangesAsync(); //Ejecuta el INSERT en la base de datos.

            return Ok(transaction); //Devuelve la transacción creada con 200.
        }

        [HttpPatch("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] TransactionUpdateRequest request) //Actualiza solo los campos que vienen
        {
            var transaction = await _context.Transactions.FindAsync(id); //Busca en la base de datos la transacción que tenga ese id
            if (transaction == null) return NotFound(); //Si no la encontró, devuelve que no existe

            if (request.CryptoCode != null) transaction.CryptoCode = request.CryptoCode; //Solo actualiza CryptoCode si viene en el request
            if (request.Action != null) transaction.Action = request.Action; //Solo actualiza Action si viene en el request
            if (request.CryptoAmount.HasValue) transaction.CryptoAmount = request.CryptoAmount.Value; //Solo actualiza CryptoAmount si viene. .HasValue es porque es nullable (decimal?).
            if (request.Money.HasValue) transaction.Money = request.Money.Value; //Solo actualiza Money si viene.
            if (request.DateTime.HasValue) transaction.DateTime = request.DateTime.Value; //Solo actualiza DateTime si viene.

            await _context.SaveChangesAsync(); //Guarda los cambios en la base de datos
            return Ok(transaction); //Devuelve la transacción actualizada 
        }

        [HttpDelete("{id}")] //Define que este endpoint responde a peticiones DELETE y recibe un id en la URL.
        public async Task<IActionResult> Delete(int id) //Define el método. Recibe el id que vino en la URL como parámetro
        {
            var transaction = await _context.Transactions.FindAsync(id); //Busca en la base de datos la transacción con ese id
            if (transaction == null) return NotFound(); //Si no la encontró devuelve 404.

            _context.Transactions.Remove(transaction); //La marca para eliminar
            await _context.SaveChangesAsync(); //Ejecuta el DELETE en la base de datos
            return Ok(); //Devuelve 200 confirmando que se eliminó.
        }

        [HttpGet("analysis")] //Define que este endpoint responde a peticiones GET en /transactions/analysis
        public async Task<IActionResult> GetAnalysis() //Define el método que calcula el estado de la cartera
        {
            var transactions = await _context.Transactions.ToListAsync(); //Trae todas las transacciones
            var cryptos = new[] { "btc", "eth", "usdc" }; //Define las tres criptos a analizar.
            var resultado = new List<object>(); //Inicializa la lista de resultados y el total en cero.
            decimal totalGeneral = 0;

            foreach (var crypto in cryptos) //Recorre cada cripto
            {
                var saldo = transactions //Empieza a calcular el saldo usando la lista de transacciones
                    .Where(t => t.CryptoCode == crypto && t.Action == "purchase") //Filtra solo las compras de esa cripto.
                    .Sum(t => t.CryptoAmount) //Suma todas las cantidades compradas
                    - transactions //Le resta las ventas.
                    .Where(t => t.CryptoCode == crypto && t.Action == "sale") //Filtra solo las ventas de esa cripto.
                    .Sum(t => t.CryptoAmount); //Suma todas las cantidades vendidas.

                if (saldo > 0) //Solo procesa si tenés saldo positivo.
                {
                    decimal precio = await GetPrecioCripto(crypto); //Obtiene el precio actual.
                    decimal valorTotal = saldo * precio; //Multiplica el saldo por el precio y guarda el resultado en valorTotal.
                    totalGeneral += valorTotal; //Le suma valorTotal al total general acumulado.

                    resultado.Add(new //Agrega un objeto nuevo a la lista resultado.
                    {
                        crypto_code = crypto, //Guarda el código de la cripto.
                        cantidad = saldo, //Guarda el saldo de esa cripto.
                        dinero = valorTotal //Guarda el valor en pesos.
                    });
                }
            }

            return Ok(new { cartera = resultado, total = totalGeneral }); //Devuelve la cartera completa y el total.
        }

        private async Task<decimal> GetSaldo(string cryptoCode) //Método privado que calcula el saldo de una cripto.
        {
            var compras = await _context.Transactions //Consulta la tabla de transacciones
                .Where(t => t.CryptoCode == cryptoCode && t.Action == "purchase") //Filtra solo las compras de esa cripto.
                .ToListAsync(); //Ejecuta la consulta y devuelve una lista.

            var ventas = await _context.Transactions //Consulta la tabla de transacciones de nuevo.
                .Where(t => t.CryptoCode == cryptoCode && t.Action == "sale") //Filtra solo las ventas de esa cripto.
                .ToListAsync(); //Ejecuta la consulta y devuelve una lista.

            return compras.Sum(t => t.CryptoAmount) - ventas.Sum(t => t.CryptoAmount); //Devuelve compras menos ventas.
        }

        private async Task<decimal> GetPrecioCripto(string cryptoCode) //Método privado que consulta el precio en CriptoYa.
        {
            try //Inicia el bloque try por si la API falla.
            {
                var url = $"https://criptoya.com/api/satoshitango/{cryptoCode}/ars/1"; //Arma la URL con el código de la cripto.
                var response = await _httpClient.GetFromJsonAsync<CriptoYaResponse>(url); //Llama a la API y deserializa la respuesta.
                return response?.TotalBid ?? 0; //Devuelve el precio. Si la respuesta es null devuelve 0.
            }
            catch //Si la API falla devuelve 0 sin crashear la app
            {
                return 0;
            }
        }
    }

    public class TransactionRequest //Define la clase que representa los datos que manda el frontend al crear una transacción.
    {
        public string CryptoCode { get; set; } = string.Empty; //String que guarda el código de la criptomoneda. El { get; set; } significa que se puede leer y escribir. El = string.Empty la inicializa como texto vacío para evitar que sea null por defecto.
        public string Action { get; set; } = string.Empty;  //string que guarda la acción de la transacción. Solo puede ser "purchase" o "sale". También inicializada en vacío por la misma razón.
        public decimal CryptoAmount { get; set; } //Decimal que guarda la cantidad de cripto. 
        public DateTime DateTime { get; set; } //DateTime que guarda la fecha y hora exacta de la transacción
    }

    public class TransactionUpdateRequest //Define una clase separada para cuando el usuario quiere editar una transacción existente. Es distinta a TransactionRequest porque todos sus campos son opcionales, el usuario puede mandar solo los campos que quiere cambiar.
    {
        public string? CryptoCode { get; set; } //El código de la cripto. El ? significa que puede ser null.
        public string? Action { get; set; } //La acción. También puede ser null.
        public decimal? CryptoAmount { get; set; } //La cantidad. También puede ser null.
        public decimal? Money { get; set; } //El monto. También puede ser null.
        public DateTime? DateTime { get; set; } //La fecha. También puede ser null.
    }

    public class CriptoYaResponse //Define la clase que representa la respuesta de la API de CriptoYa.
    {
        public decimal Ask { get; set; } //El precio al que el exchange vende la cripto, sin incluir la comisión.
        public decimal TotalAsk { get; set; } //El precio al que el exchange vende la cripto, con la comisión incluida.
        public decimal Bid { get; set; } //El precio al que el exchange compra la cripto, sin incluir la comisión.
        public decimal TotalBid { get; set; } //El precio al que el exchange compra la cripto, con la comisión incluida. Este es el que usa el código para calcular el valor real de la transacción porque refleja el precio final que paga el usuario.
    }
}