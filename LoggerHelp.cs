using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiniMcs_ks
{
    public class LoggerHelp
    {
        static LoggerHelp _loggerHelp;
        public static LoggerHelp GetLogger()
        {
            _loggerHelp ??= new LoggerHelp();
            return _loggerHelp;
        }

        private readonly ILogger logger;
        private LoggerHelp()
        {
            logger = LogManager.GetCurrentClassLogger();
        }

        /// <summary>
        /// 记录异常日志
        /// </summary>
        /// <param name="error"></param>
        /// <param name="ex"></param>
        /// <param name="writeConsole"></param>
        public void LogError(string error, Exception ex)
        {
            logger.Error(ex, error);
        }

        /// <summary>
        /// 记录信息日志
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="writeConsole"></param>
        public void LogInfo(string msg)
        {
            logger.Info(msg);
        }

        /// <summary>
        /// 记录信息日志
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="writeConsole"></param>
        public void LogWarn(string msg)
        {
            logger.Warn(msg);
        }
    }
}
