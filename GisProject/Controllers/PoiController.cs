using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Npgsql;

namespace YourNamespace.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PoiController : ControllerBase
    {
        private readonly string _connectionString = "Host=localhost;Database=PosGis;Username=postgres;Password=123";
        [HttpGet]
        public IActionResult Get()
        {
            return Ok();
        }
        [HttpPost]
        public IActionResult ImportPoiData()
        {
            try
            {
                var geoJsonFilePath = "C:\\Users\\Mahir\\Desktop\\data\\poi.geojson";
                var geoJsonContent = System.IO.File.ReadAllText(geoJsonFilePath);

                JObject jObject = JObject.Parse(geoJsonContent);
                JArray features = (JArray)jObject["features"];

                using (NpgsqlConnection connection = new NpgsqlConnection(_connectionString))
                {
                    connection.Open();

                    foreach (JObject feature in features)
                    {
                        
                        JArray coordinates = (JArray)feature["geometry"]["coordinates"];
                        double longitude = (double)coordinates[0];
                        double latitude = (double)coordinates[1];


                        
                        string sql = "INSERT INTO poi2 (geometry) VALUES (ST_SetSRID(ST_MakePoint(@longitude, @latitude), 4326))";

                        
                        using (NpgsqlCommand command = new NpgsqlCommand(sql, connection))
                        {
                            command.Parameters.AddWithValue("longitude", longitude);
                            command.Parameters.AddWithValue("latitude", latitude);

                            command.ExecuteNonQuery();
                        }
                    }
                }
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest("Failed to import POI data: " + ex.Message);
            }
        }
    }


    
}