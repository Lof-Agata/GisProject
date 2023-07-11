using Microsoft.AspNetCore.Mvc;
using NetTopologySuite.Geometries;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Npgsql;

namespace GisProject.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class RoadController : ControllerBase
    {
        private readonly string _connectionString = "Host=localhost;Database=PosGis;Username=postgres;Password=123";
        [HttpGet]
        public IActionResult Get()
        {
            return Ok();
        }
        [HttpPost]
        public IActionResult ImportRoadData()
        {
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                connection.Open();
                string jsonFilePath = "C:\\Users\\Mahir\\Desktop\\data\\yollar.geojson";

                string json = System.IO.File.ReadAllText(jsonFilePath);
                JObject featureCollection = JsonConvert.DeserializeObject<JObject>(json);
                JArray features = (JArray)featureCollection["features"];



                foreach (var feature in features)
                {
                    var geometry = feature["geometry"].ToString();
                    var coordinates = ((JArray)feature["geometry"]["coordinates"]).ToObject<double[][]>();

                    var lineString = new LineString(coordinates.Select(c => new Coordinate(c[0], c[1])).ToArray());

                    var commandText = "SELECT COUNT(*) FROM road WHERE ST_Intersects(geometry, ST_GeomFromText(@lineString, 4326))";
                    using var command = new NpgsqlCommand(commandText, connection);
                    command.Parameters.AddWithValue("lineString", lineString.ToText());
                    long intersectCount = (long)command.ExecuteScalar();
                    int intersectCountInt = (int)intersectCount;

                    if (intersectCount == 0)
                    {
                        commandText = "INSERT INTO road (geometry) VALUES (ST_GeomFromText(@lineString, 4326))";
                        command.CommandText = commandText;
                        command.ExecuteNonQuery();
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

