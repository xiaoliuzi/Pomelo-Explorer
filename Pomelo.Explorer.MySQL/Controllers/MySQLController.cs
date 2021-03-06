﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using Pomelo.Explorer.Definitions;
using Pomelo.Explorer.MySQL.Models;

namespace Pomelo.Explorer.MySQL.Controllers
{
    public class MySQLController : Controller
    {
        [HttpPost]
        public IActionResult CreateConnection([FromBody]CreateConnectionRequest request)
        {
            var client = new MySqlConnection($"Server={request.Address}; Port={request.Port}; Uid={request.Username}; Pwd={request.Password}; Pooling=False");
            var timestamp = DateTime.UtcNow.Ticks.ToString();
            ConnectionHelper.Connections.Add(timestamp, client);
            return Json(new CreateConnectionResponse 
            {
                Id = timestamp
            });
        }

        [HttpPost]
        public async Task<IActionResult> OpenConnection(string id)
        {
            if (!ConnectionHelper.Connections.ContainsKey(id))
            {
                return NotFound(id);
            }

            try
            {
                await ConnectionHelper.Connections[id].OpenAsync();
            }
            catch (MySqlException ex)
            {
                Response.StatusCode = 400;
                return Json(new DBError 
                {
                    Code = ex.Number,
                    Message = ex.Message
                });
            }

            return Json("OK");
        }

        [HttpPost]
        public async Task<IActionResult> TestConnection([FromBody]CreateConnectionRequest request)
        {
            using (var client = new MySqlConnection($"Server={request.Address}; Port={request.Port}; Uid={request.Username}; Pwd={request.Password}; Pooling=False"))
            {
                try
                {
                    await client.OpenAsync();
                }
                catch (MySqlException ex)
                {
                    Response.StatusCode = 400;
                    return Json(new DBError
                    {
                        Code = ex.Number,
                        Message = ex.Message
                    });
                }
                return Json("OK");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetDatabases(string id)
        {
            var conn = ConnectionHelper.Connections[id];
            using (var command = new MySqlCommand("SHOW DATABASES;", ConnectionHelper.Connections[id]))
            {
                await conn.EnsureOpenedAsync();
                var result = new List<string>();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        result.Add(reader["Database"].ToString());
                    }
                }
                return Json(result); 
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetTables(string id, string database)
        {
            var conn = ConnectionHelper.Connections[id];
            using (var command = new MySqlCommand($"SHOW TABLES IN `{database}`;", ConnectionHelper.Connections[id]))
            {
                await conn.EnsureOpenedAsync();
                var result = new List<TableInfo>();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        result.Add(new TableInfo 
                        {
                            Name = reader[0].ToString(),
                            Charset = "utf8mb4",
                            Collate = "utf8mb4_general-ci",
                            Columns = 0,
                            Records = 0,
                            Engine = "InnoDB"
                        });
                    }
                }
                return Json(result);
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetTableRows([FromRoute]string id, [FromBody]ViewTableRowsRequest request)
        {
            const int pageSize = 1000;
            var condition = "";
            if (request.Expression != null)
            {
                condition = "WHERE " + MySqlConditionExpressionTranslator.GenerateSql(request.Expression);
            }
            var sql = $"SELECT * FROM `{request.Database}`.`{request.Table}` {condition} LIMIT {pageSize} OFFSET {request.Page * pageSize}";

            var conn = ConnectionHelper.Connections[id];
            using (var command = new MySqlCommand(sql, ConnectionHelper.Connections[id]))
            {
                await conn.EnsureOpenedAsync();
                var result = new List<List<string>>();
                var columns = new List<string>();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    for (var i = 0; i < reader.FieldCount; ++i)
                    {
                        columns.Add(reader.GetName(i));
                    }

                    while (await reader.ReadAsync())
                    {
                        var row = new List<string>();
                        for (var i = 0; i < reader.FieldCount; ++i)
                        {
                            row.Add(reader[i].ToString());
                        }
                        result.Add(row);
                    }
                }
                return Json(new TableResponse
                { 
                    Columns = columns,
                    Values = result
                });
            }
        }
    }
}
