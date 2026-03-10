using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using Dapper;
using System.Linq;

namespace BreakfastApp
{
    public class DapperSql<T> where T : class
    {
        protected string ConnectionString = @"Server=.\SQL2022;Database=BreakfastDB;User Id=sa;Password=1qaz@wsx;TrustServerCertificate=True;";
        protected IDbConnection Connection => new SqlConnection(ConnectionString);

        public List<T> ReadAll(string sql, object? parameters = null)
        {
            using var db = Connection;
            return db.Query<T>(sql, parameters).ToList();
        }

        public T? ReadSingle(string sql, object? parameters = null)
        {
            using var db = Connection;
            return db.QueryFirstOrDefault<T>(sql, parameters);
        }

        public int Execute(string sql, object? parameters = null)
        {
            using var db = Connection;
            return db.Execute(sql, parameters);
        }

        public int Insert(T entity)
        {
            return 0;
        }
    }
}
