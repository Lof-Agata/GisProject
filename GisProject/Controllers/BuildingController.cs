using Microsoft.AspNetCore.Mvc;
using NetTopologySuite;
using NetTopologySuite.Features;
using NetTopologySuite.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Npgsql;
using System.Text.Json;

namespace GisProject.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class BuildingController : ControllerBase
    {
        private readonly string _connectionString = "Host=localhost;Database=PosGis;Username=postgres;Password=123";
        [HttpGet("get/{id}")]
        public IActionResult Get(int id)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();
            var featureCollection = new FeatureCollection();

            var commandText = "SELECT id, ST_AsText(geometry) FROM building WHERE id = @id";
            using var command = new NpgsqlCommand(commandText, connection);
            command.Parameters.AddWithValue("@id", id);
            using var reader = command.ExecuteReader();

            var wktReader = new WKTReader();

            while (reader.Read())
            {
                if (!reader.IsDBNull(1))
                {
                    var buildingId = reader.GetInt32(0);
                    var geometryText = reader.GetString(1);

                    var geometry = wktReader.Read(geometryText);
                    var feature = new Feature(geometry, new AttributesTable { { "id", buildingId } });
                    featureCollection.Add(feature);
                }
            }

            var geoJsonWriter = new GeoJsonWriter();
            var geoJson = geoJsonWriter.Write(featureCollection);

            return Ok(geoJson);
        }

        [HttpGet("Get")]
        public IActionResult Get()
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();
            var featureCollection = new FeatureCollection();

            var commandText = "SELECT id, ST_AsText(geometry) FROM building";
            using var command = new NpgsqlCommand(commandText, connection);
            using var reader = command.ExecuteReader();

            var wktReader = new WKTReader();

            while (reader.Read())
            {
                if (!reader.IsDBNull(1))
                {
                    var id = reader.GetInt32(0);
                    var geometryText = reader.GetString(1);

                    var geometry = wktReader.Read(geometryText);
                    var feature = new Feature(geometry, new AttributesTable { { "id", id } });
                    featureCollection.Add(feature);
                }
            }

            var geoJsonWriter = new GeoJsonWriter();
            var geoJson = geoJsonWriter.Write(featureCollection);

            return Ok(geoJson);
        }
        [HttpPost("ImportBuildingData")]
        public IActionResult ImportBuildingData()
        {
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                connection.Open();
                string jsonFilePath1 = "C:\\Users\\Mahir\\Desktop\\data2\\bina1.geojson";
                string jsonFilePath2 = "C:\\Users\\Mahir\\Desktop\\data2\\bina2.geojson";

                string json1 = System.IO.File.ReadAllText(jsonFilePath1);
                string json2 = System.IO.File.ReadAllText(jsonFilePath2);

                JObject featureCollection1 = JsonConvert.DeserializeObject<JObject>(json1);
                JObject featureCollection2 = JsonConvert.DeserializeObject<JObject>(json2);

                JArray features1 = (JArray)featureCollection1["features"];
                JArray features2 = (JArray)featureCollection2["features"];

                HashSet<string> uniqueGeometries = new HashSet<string>();

                foreach (var feature1 in features1)
                {
                    var geometry1 = feature1["geometry"].ToString();

                    uniqueGeometries.Add(geometry1);
                }

                foreach (var feature2 in features2)
                {
                    var geometry2 = feature2["geometry"].ToString();

                    uniqueGeometries.Add(geometry2);
                }

                foreach (var geometry in uniqueGeometries)
                {
                    var commandText = "INSERT INTO building2 (geometry) VALUES (@geometry)";
                    using var command = new NpgsqlCommand(commandText, connection);
                    command.Parameters.AddWithValue("geometry", geometry);
                    command.ExecuteNonQuery();
                }

                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest("Failed to import POI data: " + ex.Message);
            }
        }
        [HttpGet("getPoi/{id}")]
        public IActionResult GetPoi(int id)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();
            var commandText = @"SELECT poi2.*, ST_AsBinary(poi2.geometry) as geometry_bytes FROM poi2 
                    WHERE EXISTS (SELECT 1 FROM building WHERE ST_Contains(building.geometry, poi2.geometry) AND building.id = @id)";
            using (var command = new NpgsqlCommand(commandText, connection))
            {
                command.Parameters.AddWithValue("@id", id);

                var poiFeatures = new List<Feature>();

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var wkbReader = new WKBReader();
                        var wkbBytes = (byte[])reader["geometry_bytes"];
                        var geometry = wkbReader.Read(wkbBytes);

                        var attributes = new AttributesTable();
                        for (var i = 0; i < reader.FieldCount; i++)
                        {
                            var columnName = reader.GetName(i);
                            if (columnName != "geometry" && columnName != "geometry_bytes")
                            {
                                attributes.Add(columnName, reader[columnName]);
                            }
                        }

                        var poiFeature = new Feature(geometry, attributes);
                        poiFeatures.Add(poiFeature);
                    }
                }

                var featureCollection = new FeatureCollection();
                foreach (var poiFeature in poiFeatures)
                {
                    featureCollection.Add(poiFeature);
                }

                var geoJsonWriter = new GeoJsonWriter();
                var geoJson = geoJsonWriter.Write(featureCollection);

                return Ok(geoJson);
            }
        }
        [HttpPost("AddData")]
        public IActionResult AddData([FromBody] JsonElement body)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();
            var geoJsonContent = body.GetRawText();
            var geoJsonReader = new GeoJsonReader();
            var feature = geoJsonReader.Read<Feature>(geoJsonContent);

            var geometry = feature.Geometry;
            var geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
            var wkbWriter = new WKBWriter();
            var wkbBytes = wkbWriter.Write(geometry);

            var commandText = "INSERT INTO building (geometry) VALUES (@geometry)";
            using var command = new NpgsqlCommand(commandText, connection);
            command.Parameters.AddWithValue("@geometry", wkbBytes);
            command.ExecuteNonQuery();



            return Ok("building add");
        }

        [HttpDelete("{id}")]
        public IActionResult Delete(int id)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();
            var commandText = "DELETE FROM building WHERE id = @id";
            using var command = new NpgsqlCommand(commandText, connection);
            command.Parameters.AddWithValue("@id", id);
            command.ExecuteNonQuery();

            return Ok("building delete");
        }
        [HttpPut("{id}")]
        public IActionResult Update(int id)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();
            string geoJsonFilePath = "C:\\Users\\Mahir\\Desktop\\data\\bina-update.geojson";
            string geoJsonContent = System.IO.File.ReadAllText(geoJsonFilePath);

            var geoJsonReader = new GeoJsonReader();
            var feature = geoJsonReader.Read<Feature>(geoJsonContent);

            
            var geometry = feature.Geometry;

            
            var geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
            var wkbWriter = new WKBWriter();
            byte[] wkbBytes = wkbWriter.Write(geometry);

            var commandText = "UPDATE building SET geometry = @geometry WHERE id = @id";
            using var command = new NpgsqlCommand(commandText, connection);
            command.Parameters.AddWithValue("@geometry", wkbBytes);
            command.Parameters.AddWithValue("@id", id);
            command.ExecuteNonQuery();

            return Ok("building update");
        }

    }
}

