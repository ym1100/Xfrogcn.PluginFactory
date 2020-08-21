﻿using System;
using System.Collections.Generic;
using System.IO;
using PluginFactory.Abstractions;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace PluginFactory
{
    /// <summary>
    /// 默认的插件载入器
    /// </summary>
    public class DefaultPluginLoader : IPluginLoader
    {
        readonly PluginFactoryOptions _options;
        readonly IServiceCollection _services;

        public DefaultPluginLoader(PluginFactoryOptions options, IServiceCollection services)
        {
            _options = options;
            _services = services;
        }

        private List<PluginInfo> _pluginList = new List<PluginInfo>();
        public IReadOnlyList<PluginInfo> PluginList => _pluginList;

        public virtual void Load()
        {
            var dir = _options.FileProvider.GetDirectoryContents(string.Empty);
            if (!dir.Exists)
            {
                return;
            }

            lock (_pluginList)
            {
                List<PluginInfo> list = null;
                foreach (var p in dir)
                {
                    if (p.IsDirectory)
                    {
                        // 隔离插件
                        var pluginDir = _options.FileProvider.GetDirectoryContents(p.Name);
                        foreach (var pd in pluginDir)
                        {
                            if (pd.IsDirectory)
                            {
                                continue;
                            }
                            string fileName = Path.GetFileNameWithoutExtension(pd.PhysicalPath);
                            if (fileName.Equals(p.Name, StringComparison.OrdinalIgnoreCase))
                            {
                                // 插件程序集
                                list = LoadPluginFromAssembly(pd.PhysicalPath);
                            }

                        }
                    }
                    else if (p.PhysicalPath != null && Path.GetExtension(p.PhysicalPath) == ".dll")
                    {
                        //
                        list = LoadPluginFromAssembly(p.PhysicalPath);
                    }
                }
                if(list!=null && list.Count > 0)
                {
                    _pluginList.AddRange(list);
                }
            }
        }

        protected virtual List<PluginInfo> LoadPluginFromAssembly(string assemblyPath)
        {
            IsolationAssemblyLoadContext context = new IsolationAssemblyLoadContext(assemblyPath);
            var assembly = context.Load();
            if (assembly == null)
            {
                // 异常
                throw new Exception();
            }

            var types = assembly.GetExportedTypes();
            List<PluginInfo> plist = new List<PluginInfo>();
            foreach(Type t in types)
            {
                PluginInfo pi = LoadPluginFromType(t);
                if(pi != null)
                {
                    plist.Add(pi);
                }
            }

            return plist;
        }

        protected virtual PluginInfo LoadPluginFromType(Type type)
        {
            Type[] iTypes = type.GetInterfaces();
            if(iTypes==null || iTypes.Length == 0)
            {
                return null;
            }

            PluginInfo pi = null;
            if (typeof(IPlugin).IsAssignableFrom(type))
            {
                pi = new PluginInfo()
                {
                    PluginType = type,
                    CanInit = false,
                    CanConfig = false
                };
            }
            if(pi == null)
            {
                return null;
            }

            var attr = type.GetCustomAttributes(typeof(PluginAttribute), false).OfType<PluginAttribute>().FirstOrDefault();
            if(attr != null)
            {
                pi.Id = attr.Id;
                pi.Name = attr.Name;
                pi.Alias = attr.Alias;
                pi.Description = attr.Description;
            }
            pi.Id = string.IsNullOrEmpty(pi.Id) ? type.FullName : pi.Id;
            pi.Name = string.IsNullOrEmpty(pi.Name) ? type.FullName : pi.Id;

            // 初始化
            if(typeof(ISupportInitPlugin).IsAssignableFrom(type))
            {
                pi.CanInit = true;
            }

            // 配置
            Type cfgType = iTypes.FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(ISupportConfigPlugin<>));
            if (cfgType != null)
            {
                pi.ConfigType = cfgType.GenericTypeArguments[0];
                pi.CanConfig = true;
            }

            return pi;
        }


        public virtual void Init()
        {
            var initList = _pluginList.Where(x => x.CanInit && x.IsEnable).ToList();
            if (initList.Count == 0)
            {
                return;
            }

            IPluginInitContext initContext = new PluginInitContext(_options.PluginPath, this, _services);
            foreach(PluginInfo pi in initList)
            {
                ISupportInitPlugin plugin = Activator.CreateInstance(pi.PluginType) as ISupportInitPlugin;
                plugin.Init(initContext);
            }

        }

        
    }
}