﻿using Common;
using Logger.DataBase.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Logger.DataBase
{
    public class DataBaseLoggerProvider : ILoggerProvider
    {

        private readonly LoggerConfiguration loggerConfiguration;

        private readonly SnowflakeHelper snowflakeHelper;

        private readonly ConcurrentDictionary<string, DataBaseLogger> loggers = new ();


        public DataBaseLoggerProvider(IOptionsMonitor<LoggerConfiguration> config, SnowflakeHelper snowflakeHelper)
        {
            loggerConfiguration = config.CurrentValue;
            this.snowflakeHelper = snowflakeHelper;
        }



        public ILogger CreateLogger(string categoryName)
        {
            return loggers.GetOrAdd(categoryName, new DataBaseLogger(categoryName, loggerConfiguration, snowflakeHelper));
        }

        public void Dispose()
        {
            loggers.Clear();
        }
    }
}
