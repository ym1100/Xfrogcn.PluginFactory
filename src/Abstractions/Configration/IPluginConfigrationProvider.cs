﻿using Microsoft.Extensions.Configuration;

namespace PluginFactory
{
    /// <summary>
    /// Plugin配置节点获取器
    /// </summary>
    /// <typeparam name="TPlugin"></typeparam>
    public interface IPluginConfigrationProvider<TPlugin>
    {
        public IConfiguration Configuration { get; }
    }
}
