﻿// -----------------------------------------------------------------------
// <copyright file="HandleBootstrapper.cs" company="">
// Copyright (c) 2011-2012 Bernhard Schwarz, Florian Lembeck
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// </copyright>
// -----------------------------------------------------------------------

namespace Handle.WPF
{
  using System;
  using System.Collections.Generic;
  using System.ComponentModel.Composition;
  using System.ComponentModel.Composition.Hosting;
  using System.ComponentModel.Composition.Primitives;
  using System.IO;
  using System.Linq;
  using Caliburn.Micro;
  using IronRuby;

  public class HandleBootstrapper : Bootstrapper<IShell>
  {
    private CompositionContainer container;

    /// <summary>
    /// Use MEF
    /// </summary>
    protected override void Configure()
    {
      var catalog = new AggregateCatalog(AssemblySource.Instance.Select(x => new AssemblyCatalog(x)).OfType<ComposablePartCatalog>());
      this.container = new CompositionContainer(catalog);
      var batch = new CompositionBatch();

      DirectoryInfo di = new DirectoryInfo(Settings.PATH);
      if (!di.Exists)
        di.Create();

      EventAggregator eventAggregator = new EventAggregator();
      FilterService fs = new FilterService();
      eventAggregator.Subscribe(fs);
      Settings settings = Settings.Load();
      fs.Settings = settings;
      fs.PopulatePatterns();

      batch.AddExportedValue<IWindowManager>(new WindowManager());
      batch.AddExportedValue<IEventAggregator>(eventAggregator);
      batch.AddExportedValue<IProgressService>(new ProgressService());
      batch.AddExportedValue<Settings>(settings);
      batch.AddExportedValue<FilterService>(fs);

      batch.AddExportedValue(this.container);
      batch.AddExportedValue(catalog);
      
      this.container.Compose(batch);
    }

    protected override object GetInstance(Type serviceType, string key)
    {
      string contract = string.IsNullOrEmpty(key) ? AttributedModelServices.GetContractName(serviceType) : key;
      var exports = this.container.GetExportedValues<object>(contract);

      if (exports.Count() > 0)
      {
        return exports.First();
      }

      throw new Exception(string.Format("Could not locate any instances of contract {0}.", contract));
    }

    protected override IEnumerable<object> GetAllInstances(Type serviceType)
    {
      return this.container.GetExportedValues<object>(AttributedModelServices.GetContractName(serviceType));
    }

    protected override void BuildUp(object instance)
    {
      this.container.SatisfyImportsOnce(instance);
    }

    private void LoadPlugins()
    {
      var engine = Ruby.CreateEngine();
      var scope = engine.CreateScope();
      var paths = engine.GetSearchPaths().ToList();
      paths.Add(Settings.PATH + "plugins");
      engine.Execute("plugin.rb");
    }
  }
}
