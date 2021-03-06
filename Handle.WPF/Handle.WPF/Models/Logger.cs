﻿// -----------------------------------------------------------------------
// <copyright file="Logger.cs" company="">
// Copyright (c) 2011 Bernhard Schwarz, Florian Lembeck
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
  using System.IO;

  /// <summary>
  /// Represents a basic logger implementation for chat procotols with a queue to keep everything in order.
  /// </summary>
  public class Logger : IDisposable
  {
    private Queue<string> logQueue;
    private const double MaxLogAge = 0.0;
    private const int QueueSize = 3;
    private DateTime lastFlushed;
    private FileStream fs;
    private StreamWriter sw;

    /// <summary>
    /// Initializes a new instance of the Logger class.
    /// </summary>
    /// <param name="path">Specifies the file path</param>
    public Logger(string path)
    {
      logQueue = new Queue<string>();
      fs = new FileStream(path, FileMode.Append, FileAccess.Write);
      sw = new StreamWriter(fs);
      sw.AutoFlush = true;
      lastFlushed = DateTime.Now;
    }

    public void Dispose()
    {
      flushLog();
      sw.Close();
      fs.Close();
    }

    /// <summary>
    /// Adds a new message to the log
    /// </summary>
    /// <param name="message">The message to be added</param>
    public void Append(string message)
    {
      new Action<string>(writeToLog).BeginInvoke(message, null, null);
    }

    private void writeToLog(string message)
    {
      lock (this.logQueue)
      {
        this.logQueue.Enqueue(message);
        if (this.logQueue.Count >= QueueSize || shouldDoPeriodicFlush())
        {
          flushLog();
        }
      }
    }

    private bool shouldDoPeriodicFlush()
    {
      return (DateTime.Now - lastFlushed).TotalSeconds >= MaxLogAge;
    }

    private void flushLog()
    {
      while (logQueue.Count > 0)
      {
        string entry = logQueue.Dequeue();
        sw.WriteLine(entry);
      }

      this.lastFlushed = DateTime.Now;
    }
  }
}