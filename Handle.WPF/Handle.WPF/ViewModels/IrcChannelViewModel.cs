﻿// -----------------------------------------------------------------------
// <copyright file="IrcChannelViewModel.cs" company="">
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
  using System.ComponentModel.Composition;
  using System.IO;
  using System.Media;
  using System.Text;
  using System.Threading;
  using System.Windows;
  using System.Windows.Input;
  using System.Windows.Interop;
  using System.Windows.Threading;
  using Caliburn.Micro;
  using IrcDotNet;
  using System.Windows.Controls;
  using System.Text.RegularExpressions;

  /// <summary>
  /// TODO: Update summary.
  /// </summary>
  public class IrcChannelViewModel : ViewModelBase
  {
    public delegate void JoinChannelClickedEventHandler();
    public event JoinChannelClickedEventHandler JoinChannelClicked;

    private IEventAggregator events;
    private List<string> personalHistory;
    private int historyIndex = -1;
    private Queue<string> tabCompletionQueue;
    private string tabCompletionName;

    public IrcChannel Channel { get; set; }

    public BindableCollection<Message> Messages { get; set; }

    private Logger logger;
    private string message;
    public string Message
    {
      get
      {
        return this.message;
      }
      set
      {
        this.message = value;
        NotifyOfPropertyChange(() => this.Message);
        NotifyOfPropertyChange(() => this.CanSend);
      }
    }
    private string topic;

    public string Topic
    {
      get
      {
        return topic;
      }
      set
      {
        this.topic = value;
        NotifyOfPropertyChange(() => this.Topic);
      }
    }

    public bool CanSend
    {
      get
      {
        return !string.IsNullOrWhiteSpace(this.Message);
      }
    }

    public bool Closable { get; set; }

    /// <summary>
    /// Initializes a new instance of the IrcChannelViewModel class
    /// </summary>
    public IrcChannelViewModel(IrcChannel channel, string networkName, Settings settings)
    {
      this.Settings = settings;
      this.Messages = new BindableCollection<Message>();
      this.Closable = true;
      this.DisplayName = channel.Name;
      this.networkName = networkName;
      this.personalHistory = new List<string>();
      this.events = IoC.Get<IEventAggregator>();
      this.Channel = channel;
      this.Channel.ModesChanged += this.channelModesChanged;
      this.Channel.UsersListReceived += this.channelUsersListReceived;
      this.Channel.MessageReceived += this.channelMessageReceived;
      this.Channel.UserJoined += this.channelUserJoined;
      this.Channel.UserLeft += this.channelUserLeft;
      this.Channel.NoticeReceived += this.channelNoticeReceived;
      this.Channel.TopicChanged += this.channelTopicChanged;
      this.Channel.Client.WhoIsReplyReceived += this.whoIsReplyReceived;

      DirectoryInfo di = new DirectoryInfo(Settings.PATH + "\\logs\\");
      if (!di.Exists)
        di.Create();
      if (this.Settings.CanLog)
        this.logger = new Logger(String.Format("{0}\\logs\\{1}.{2}.txt",
                                 Settings.PATH,
                                 channel.Name,
                                 networkName));

      this.Channel.GetTopic();
    }

    private void channelUsersListReceived(object sender, EventArgs e)
    {
      StringBuilder sb = new StringBuilder();
      foreach (var user in this.Channel.Users)
      {
        sb.Append(user.User.NickName + " | ");
      }

      if (sb[sb.Length - 2] == '|')
        sb.Length -= 3;
      this.Users = sb.ToString();
    }

    private void channelModesChanged(object sender, EventArgs e)
    {
    }

    private void whoIsReplyReceived(object sender, IrcUserEventArgs e)
    {
      bool isUser = false;
      foreach (IrcChannelUser icu in this.Channel.Users)
      {
        if (icu.User.NickName == e.User.NickName)
        {
          isUser = true;
        }
      }
      if (isUser)
      {
        string channels = "";
        foreach (IrcChannel ic in e.User.Client.Channels)
        {
          channels += ic.Name + " ";
        }
        Message m = new Message("[" + e.User.HostName + "]",
                                DateTime.Now.ToString(this.Settings.TimestampFormat),
                                e.User.NickName);
        this.Messages.Add(m);
        m = new Message(e.User.RealName,
                                DateTime.Now.ToString(this.Settings.TimestampFormat),
                                "realname : ");
        this.Messages.Add(m);
        m = new Message(channels,
                                DateTime.Now.ToString(this.Settings.TimestampFormat),
                                "channels : ");
        this.Messages.Add(m);
        m = new Message(e.User.Client.ServerName,
                                DateTime.Now.ToString(this.Settings.TimestampFormat),
                                "server : ");
        this.Messages.Add(m);
        m = new Message("End of WhoIs",
                                DateTime.Now.ToString(this.Settings.TimestampFormat),
                                "");
        this.Messages.Add(m);
      }
      else
      {
        Message m = new Message("No such nick: flohsrfdrr",
                                DateTime.Now.ToString(this.Settings.TimestampFormat),
                                e.User.NickName);
        this.Messages.Add(m);
        m = new Message("End of WhoIs",
                                DateTime.Now.ToString(this.Settings.TimestampFormat),
                                "");
        this.Messages.Add(m);
      }
      //if (this.Settings.CanLog && this.logger != null)
      //  logger.Append(String.Format("{0} {1}: {2}",
      //                              m.Received,
      //                              m.Sender,
      //                              m.Text));
      //this.events.Publish(new MessageFilterEventArgs(this.Channel.Name, this.networkName,
      //                                               DateTime.Now.ToString("HH:mm"), m.Sender, m.Text));
    }

    private string users;

    public string Users
    {
      get
      {
        return this.users;
      }
      set
      {
        this.users = value;
        NotifyOfPropertyChange(() => this.Users);
      }
    }

    private string displayName;
    private string networkName;

    public override string DisplayName
    {
      get
      {
        return this.displayName;
      }

      set
      {
        this.displayName = value;
      }
    }

    private void channelTopicChanged(object sender, EventArgs e)
    {
      this.Topic = this.Channel.Topic;
    }

    private void channelMessageReceived(object sender, IrcMessageEventArgs e)
    {
      Message m = new Message(e.Text,
                              DateTime.Now.ToString(this.Settings.TimestampFormat),
                              e.Source.Name);
      this.Messages.Add(m);
      if (this.Settings.CanLog && this.logger != null)
        logger.Append(String.Format("{0} {1}: {2}",
                                    m.Received,
                                    m.Sender,
                                    m.Text));
      this.events.Publish(new MessageFilterEventArgs(this.Channel.Name, this.networkName,
                                                     DateTime.Now.ToString("HH:mm"), m.Sender, m.Text));
    }

    private void channelNoticeReceived(object sender, IrcMessageEventArgs e)
    {
      this.Messages.Add(new Message(e.Text, DateTime.Now.ToString(this.Settings.TimestampFormat), "=!="));
    }

    private void channelUserJoined(object sender, IrcChannelUserEventArgs e)
    {
      if (this.Settings.ShowWelcomeMessages)
      {
        this.Messages.Add(new Message(String.Format("{0} [{1}] has joined {2}",
                                                    e.ChannelUser.User.NickName,
                                                    e.ChannelUser.User.HostName,
                                                    e.ChannelUser.Channel.Name),
                                      DateTime.Now.ToString(this.Settings.TimestampFormat),
                                      "=!="));
      }
    }

    private void channelUserLeft(object sender, IrcChannelUserEventArgs e)
    {
      if (this.Settings.ShowLeaveMessages)
      {
        this.Messages.Add(new Message(String.Format("{0} [{1}] has left {2} [{3}]",
                                                    e.ChannelUser.User.NickName,
                                                    e.ChannelUser.User.HostName,
                                                    e.ChannelUser.Channel.Name,
                                                    e.Comment),
                                      DateTime.Now.ToString(this.Settings.TimestampFormat),
                                      "=!="));
      }
    }

    public void Send()
    {
      // TODO Ignore adjacent duplicates
      this.personalHistory.Add(this.Message);
      this.historyIndex = -1;

      string command = "";
      if (this.Message.IndexOf(' ') > -1)
      {
        command = this.Message.Substring(0, this.Message.IndexOf(' '));
      }
      else
      {
        command = "Message";
      }
      switch (command)
      {
        case "/Whois":
          string[] x = this.Message.Split(' ');
          this.Channel.Client.QueryWhoIs(x[1]);
          break;
        default:
          this.Channel.Client.LocalUser.SendMessage(this.Channel, this.Message);
          Message m = new Message(this.Message.Trim(),
                                  DateTime.Now.ToString(this.Settings.TimestampFormat),
                                  this.Channel.Client.LocalUser.NickName);
          this.Messages.Add(m);
          if (this.Settings.CanLog && this.logger != null)
            logger.Append(String.Format("{0} {1}: {2}", m.Received, m.Sender, m.Text));
          this.Message = String.Empty;
          break;
      }
    }

    public void LeaveChannel(string message)
    {
      // Unsubscribe from all events
      this.Channel.ModesChanged -= this.channelModesChanged;
      this.Channel.UsersListReceived -= this.channelUsersListReceived;
      this.Channel.MessageReceived -= this.channelMessageReceived;
      this.Channel.UserJoined -= this.channelUserJoined;
      this.Channel.UserLeft -= this.channelUserLeft;
      this.Channel.NoticeReceived -= this.channelNoticeReceived;
      this.Channel.TopicChanged -= this.channelTopicChanged;

      this.Channel.Leave(message);

      if (this.Settings.CanLog && this.logger != null)
        this.logger.Dispose();
    }

    public void LeaveChannel()
    {
      this.LeaveChannel(string.Empty);
    }

    public void JoinChannel()
    {
      if (this.JoinChannelClicked != null)
        this.JoinChannelClicked();
    }

    public override IEnumerable<InputBindingCommand> GetInputBindingCommands()
    {
      yield return new InputBindingCommand(LeaveChannel)
      {
        GestureModifier = ModifierKeys.Control,
        GestureKey = Key.W
      };
    }

    public void OpenContextMenu()
    {
      var icv = GetView() as IrcChannelView;
      icv.CoMenu.PlacementTarget = icv;
      icv.CoMenu.IsOpen = true;
    }

    public void ClearMessages()
    {
      this.Messages.Clear();
    }

    public void SaveMessages()
    {
      var icv = GetView() as IrcChannelView;
      var dlg = new Microsoft.Win32.SaveFileDialog();
      dlg.DefaultExt = ".txt";
      dlg.Filter = "Text Documents (*.txt)|*.txt";
      if (dlg.ShowDialog() == true)
      {
        string filename = dlg.FileName;
        FileStream fs = new FileStream(filename, FileMode.Create);
        StreamWriter sw = new StreamWriter(fs);
        try
        {
          foreach (Message m in this.Messages)
          {
            sw.WriteLine(m.Received + ":" + m.Sender + ":" + m.Text);
          }
        }
        finally
        {
          sw.Close();
          fs.Close();
        }
      }
    }

    private void setMessageCaretPosition(int index, TextBox messageTextBox)
    {


    }

    public void MessagePreviewKeyUp(object sender, KeyEventArgs e)
    {

      if (e.Key == Key.Up)
      {
        if (this.historyIndex < this.personalHistory.Count - 1)
        {
          ++this.historyIndex;
          this.Message = this.personalHistory[this.personalHistory.Count - this.historyIndex - 1];
          (sender as TextBox).CaretIndex = this.Message.Length;
          return;
        }
      }
      else if (e.Key == Key.Down)
      {
        if (this.historyIndex > 0)
        {
          --this.historyIndex;
          this.Message = this.personalHistory[this.personalHistory.Count - this.historyIndex - 1];
          (sender as TextBox).CaretIndex = this.Message.Length;
          return;
        }
      }

      if (e.Key == Key.Tab)
      {
        var words = this.Message.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

        if (this.tabCompletionQueue == null)
        {
          this.tabCompletionName = words.Length == 1 ? words[0] : words[words.Length - 1];
          var userList = new List<string>();
          foreach (var user in this.Channel.Users)
          {
            if (user.User.NickName.StartsWith(this.tabCompletionName, StringComparison.CurrentCultureIgnoreCase))
              userList.Add(user.User.NickName);
          }

          userList.Sort();
          this.tabCompletionQueue = new Queue<string>(userList);
        }

        this.Message = this.Message.TrimEnd(' ', '\t');
        try
        {
          this.Message = this.Message.Substring(0, this.message.LastIndexOf(' ') + 1) + this.tabCompletionQueue.Dequeue();
        }
        catch
        {
          this.Message = this.Message.Substring(0, this.message.LastIndexOf(' ') + 1) + this.tabCompletionName;
          this.tabCompletionName = String.Empty;
          this.tabCompletionQueue = null;
        }

        if (words.Length == 1 && this.tabCompletionName != string.Empty)
          this.Message += ":";

        (sender as TextBox).CaretIndex = this.Message.Length;
        e.Handled = true;
      }
      else
      {
        this.tabCompletionName = string.Empty;
        this.tabCompletionQueue = null;
      }
    }
  }
}