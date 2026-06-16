using Microsoft.AspNetCore.Mvc; //Importa lo necesario para usar controladores.

namespace CryptoWallet.Controllers; //Define el espacio de nombres. Este controlador pertenece al grupo CryptoWallet.Controllers

[ApiController] //Le dice a ASP.NET que esta clase es un controlador de API.
[Route("[controller]")] //Define la ruta automáticamente según el nombre del controlador

public class WeatherForecastController : ControllerBase //Define la clase. Hereda de ControllerBase
{
    private static readonly string[] Summaries = new[] //Declara un array de strings fijo que no cambia. static significa que es compartido por todas las instancias. readonly significa que no se puede modificar después de ser creado.
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching" //Son los posibles valores del campo Summary del clima. Se usan para generar datos de prueba aleatorios.
    };

    private readonly ILogger<WeatherForecastController> _logger; //Declara una variable para el sistema de logs. Permite registrar mensajes en la consola o archivos de log.

    public WeatherForecastController(ILogger<WeatherForecastController> logger) //El constructor recibe el logger por inyección de dependencias.
    { 
        _logger = logger; //Guarda el logger en la variable privada.
    }

    [HttpGet(Name = "GetWeatherForecast")] //Define que este endpoint responde a GET. Name le da un nombre interno al endpoint
    public IEnumerable<WeatherForecast> Get() //Define el método. Devuelve una colección de objetos WeatherForecast
    {
        return Enumerable.Range(1, 5) //Genera una secuencia de números del 1 al 5. Esto hace que se generen 5 pronósticos.
        .Select(index => new WeatherForecast //Por cada número crea un objeto WeatherForecast nuevo.
        {
            Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)), //La fecha es el día de hoy más index días. Por ejemplo el primer objeto tiene mañana, el segundo pasado mañana, etc.
            TemperatureC = Random.Shared.Next(-20, 55), //Genera una temperatura aleatoria entre -20 y 55 grados Celsius.
            Summary = Summaries[Random.Shared.Next(Summaries.Length)] //Elige una descripción aleatoria del array Summaries.
        })
        .ToArray(); //Convierte la secuencia en un array y lo devuelve.
    }
}
