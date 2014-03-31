﻿using System;
using System.Configuration;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.Threading;

namespace StockReport
{
    /// 2014年3月31日22时00分49秒 6
    /// <summary>
    /// The SQLiteHelper class is intended to encapsulate high performance, 
    /// scalable best practices for common uses of SQLiteClient.
    /// </summary>
    public abstract class SQLiteHelper
    {
        //Database connection strings
        public static readonly string connStr = ConfigurationManager.ConnectionStrings["SQLiteString"].ConnectionString;

        /// <summary>
        /// 执行查询一个 SQLite 语句，返回影响的行数。
        /// </summary>
        /// <param name="cmdText">SQLite 语句</param>
        /// <param name="cmdParameters">SQLite 参数</param>
        /// <returns>受影响的行数</returns>
        public static int ExecuteNonQuery(string cmdText, params SQLiteParameter[] cmdParameters)
        {
            return ExecuteNonQuery(cmdText, CommandType.Text, cmdParameters);
        }

        /// <summary>
        /// 执行查询一个 SQLite 语句，返回影响的行数。
        /// </summary>
        /// <param name="cmdText">SQLite 语句</param>
        /// <param name="type">语句类型 默认为 Text</param>
        /// <param name="cmdParameters">SQLite 参数</param>
        /// <returns>受影响的行数</returns>
        public static int ExecuteNonQuery(string cmdText, CommandType type, params SQLiteParameter[] cmdParameters)
        {
            SQLiteCommand cmd = BuildQueryCommand(cmdText, type, cmdParameters);
            return cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// 执行一个查询，返回一个object
        /// </summary>
        /// <param name="cmdText">SQLite 语句</param>
        /// <param name="cmdParameters">SQLite 参数</param>
        /// <returns>第一行第一列的值</returns>
        public static object ExecuteScalar(string cmdText, params SQLiteParameter[] cmdParameters)
        {
            return ExecuteScalar(cmdText, CommandType.Text, cmdParameters);
        }

        /// <summary>
        /// 执行一个查询，返回一个object
        /// </summary>
        /// <param name="cmdText">SQLite 语句</param>
        /// <param name="type">语句类型 默认为 Text</param>
        /// <param name="cmdParameters">SQLite 参数</param>
        /// <returns>第一行第一列的值</returns>
        public static object ExecuteScalar(string cmdText, CommandType type, params SQLiteParameter[] cmdParameters)
        {
            SQLiteCommand cmd = BuildQueryCommand(cmdText, type, cmdParameters);
            return cmd.ExecuteScalar();
        }

        /// <summary>
        /// 执行一个查询，返回一个DataTable 数据集
        /// </summary>
        /// <param name="cmdText">SQLite 语句</param>
        /// <param name="parameters">SQLite 参数</param>
        /// <returns>DataTable 的结果集</returns>
        public static DataTable DB_Select(string cmdText, params SQLiteParameter[] cmdParameters)
        {
            return DB_Select(cmdText, CommandType.Text, cmdParameters);
        }

        /// <summary>
        /// 执行一个查询，返回一个DataTable 数据集
        /// </summary>
        /// <param name="cmdText">SQLite 语句</param>
        /// <param name="type">语句类型 默认为 Text</param>
        /// <param name="parameters">SQLite 参数</param>
        /// <returns>DataTable 的结果集</returns>
        public static DataTable DB_Select(string cmdText, CommandType type, params SQLiteParameter[] cmdParameters)
        {
            using (SQLiteDataAdapter da = new SQLiteDataAdapter())
            {
                da.SelectCommand = BuildQueryCommand(cmdText, type, cmdParameters);
                DataTable dt = new DataTable();
                da.Fill(dt);
                return dt;
            }
        }

        /// <summary>
        /// 执行存储过程
        /// </summary>
        /// <param name="storedProcName">存储过程名</param>
        /// <param name="parameters">存储过程参数</param>
        /// <param name="tableName">DataSet结果中的表名</param>
        /// <returns>DataSet</returns>
        public static DataSet RunProcedure(string storedProcName, string tableName, params SQLiteParameter[] cmdParameters)
        {
            using (SQLiteDataAdapter da = new SQLiteDataAdapter())
            {
                da.SelectCommand = BuildQueryCommand(storedProcName, CommandType.StoredProcedure, cmdParameters);
                DataSet ds = new DataSet();
                da.Fill(ds, tableName);
                return ds;
            }
        }

        /// <summary>
        /// 构建 SQLiteCommand 对象
        /// </summary>
        /// <param name="cmdText">SQLite 语句</param>
        /// <param name="cmdParameters">SQLite 参数</param>
        /// <returns>SQLiteCommand</returns>
        private static SQLiteCommand BuildQueryCommand(string cmdText, params SQLiteParameter[] cmdParameters)
        {
            return BuildQueryCommand(cmdText, CommandType.Text, cmdParameters);
        }

        /// <summary>
        /// 构建 SQLiteCommand 对象
        /// </summary>
        /// <param name="cmdText">SQLite 语句</param>
        /// <param name="type">语句类型 默认为 Text</param>
        /// <param name="cmdParameters">SQLite 参数</param>
        /// <returns>SQLiteCommand</returns>
        private static SQLiteCommand BuildQueryCommand(string cmdText, CommandType type, params SQLiteParameter[] cmdParameters)
        {
            using (SQLiteConnection conn = new SQLiteConnection(connStr))
            {
                SQLiteCommand command = new SQLiteCommand(cmdText, conn);
                command.CommandType = type;
                foreach (SQLiteParameter parameter in cmdParameters)
                {
                    if (parameter != null)
                    {
                        // 检查未分配值的输出参数,将其分配以DBNull.Value.
                        if ((parameter.Direction == ParameterDirection.InputOutput || parameter.Direction == ParameterDirection.Input) &&
                            (parameter.Value == null))
                        {
                            parameter.Value = DBNull.Value;
                        }
                        command.Parameters.Add(parameter);
                    }
                }
                conn.QuickOpen();
                return command;
            }
        }
    }

    public static class SQLiteExtensions
    {
        public static void QuickOpen(this SQLiteConnection conn)
        {
            int timeout = PublicClass.ToInt32(ConfigurationManager.AppSettings["TimeOut"]);
            timeout = Math.Max(Math.Min(30000, timeout), 3000);

            // We'll use a Stopwatch here for simplicity. A comparison to a stored DateTime.Now value could also be used
            Stopwatch sw = new Stopwatch();
            bool connectSuccess = false;

            // Try to open the connection, if anything goes wrong, make sure we set connectSuccess = false
            Thread t = new Thread(delegate()
            {
                try
                {
                    sw.Start();
                    conn.Open();
                    connectSuccess = true;
                }
                catch { }
            });

            // Make sure it's marked as a background thread so it'll get cleaned up automatically
            t.IsBackground = true;
            t.Start();

            // Keep trying to join the thread until we either succeed or the timeout value has been exceeded
            while (timeout > sw.ElapsedMilliseconds)
                if (t.Join(1))
                    break;

            // If we didn't connect successfully, throw an exception
            if (!connectSuccess)
                throw new Exception("连接超时！\r\n未能连接到数据库！\r\n如有需要，可更改App.config 中的 TimeOut 值");
        }
    }
}

