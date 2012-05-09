﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.XPath;
using System.Xml.Xsl;
using CommandLine;

namespace TransifexHelper
{
  class Program
  {
    private static Dictionary<string,DirectoryInfo> languageDirectories = new Dictionary<string, DirectoryInfo>();
    private static IniFile transifexIni = new IniFile();
    private static string targetDir = string.Empty;
    private const string ProjectSlug = "MP2";
    private const string CacheDir = "Cache";

    
    static void Main(string[] args)
    {
      Thread.CurrentThread.Name = "Main";

      // Parse Command Line options
      CommandLineOptions mpArgs = new CommandLineOptions();
      ICommandLineParser parser = new CommandLineParser(new CommandLineParserSettings(Console.Error));
      if (!parser.ParseArguments(args, mpArgs, Console.Out))
        Environment.Exit(1);
      
      if (mpArgs.Verify ^ (!mpArgs.Push && !mpArgs.Pull))
        if (mpArgs.Pull ^ (!mpArgs.Verify && !mpArgs.Push))
          if (mpArgs.Push ^ (!mpArgs.Pull && !mpArgs.Verify))
      {
        Console.WriteLine("Specify exact one of the options 'verify', 'push' or 'pull'.");
        Environment.Exit(1);
      }

      targetDir = mpArgs.TargetDir;

      if (mpArgs.Verify)
        if (!Verify())
          Environment.Exit(2);

      if (mpArgs.Push)
        if (!Push())
          Environment.Exit(3);

      if (mpArgs.Pull)
        if (!Pull())
          Environment.Exit(4);
    }

    #region Helper methods

    private static string TransifexRoot()
    {
      return targetDir + @"\Tools\Transifex";
    }

    private static string TransifexConfig()
    {
      return TransifexRoot() + @"\.tx\config";
    }

    private static string TransifexCache()
    {
      return TransifexRoot() + "\\" + CacheDir;
    }

    private static string TransifexClientExe()
    {
      return TransifexRoot() + @"\tx.exe";
    }

    private static string XsltMP2toAndroid()
    {
      return TransifexRoot() + @"\Transform-MP2 to Android.xslt";
    }

    private static string XsltAndroidtoMP2()
    {
      return TransifexRoot() + @"\Transform-Android to MP2.xslt";
    }

    #endregion

    #region Main methods

    private static bool Verify()
    {
      bool result = true;

      SearchLangDirs(new DirectoryInfo(targetDir));
      LoadTxProjectFile(TransifexConfig());

      foreach (IniFile.IniSection section in transifexIni.Sections)
      {
        if (section.Name == "main") continue;

        if (!languageDirectories.ContainsKey(section.Name.Split('.')[1]))
        {
          Console.WriteLine(
            "A value in TransifexConfig can't be found in folder structure. Please check and fix renamed or deleted project folders. {0}",
            section.Name);
          result = false;
        }
      }

      return result;
    }

    private static bool Push()
    {
      if (!Verify())
        Environment.Exit(2);

      TransformMP2toAndroid();
      UpdateTransifexConfig();
      ExecutePush();

      return true;
    }

    private static void ExecutePush()
    {
      ProcessStartInfo processStartInfo = new ProcessStartInfo();
      processStartInfo.FileName = TransifexClientExe();
      processStartInfo.Arguments = " push -s";
      processStartInfo.WorkingDirectory = TransifexRoot();

      Process process = Process.Start(processStartInfo);
      process.WaitForExit();
    }

    private static bool Pull()
    {
      if (!Verify())
        Environment.Exit(2);

      TransformAndroidToMP2();
      ExecutePull();

      return true;
    }

    private static void ExecutePull()
    {
      ProcessStartInfo processStartInfo = new ProcessStartInfo();
      processStartInfo.FileName = TransifexClientExe();
      processStartInfo.Arguments = " pull -f";
      processStartInfo.WorkingDirectory = TransifexRoot();

      Process process = Process.Start(processStartInfo);
      process.WaitForExit();
    }

    #endregion

    private static void SearchLangDirs(DirectoryInfo parentDirectory)
    {
      if (parentDirectory.Name.ToLower() == "bin") return;

      List<DirectoryInfo> subDirectories = new List<DirectoryInfo>(parentDirectory.GetDirectories());
      bool langDirFound = false;

      foreach (DirectoryInfo subDirectory in subDirectories)
      {
        // enthält language??
        if (subDirectory.Name.ToLower() != "language") continue;
        // enthält language strings_en.xml??
        if (!File.Exists(subDirectory.FullName + "\\strings_en.xml")) break;

        // füge langdir zur liste hinzu
        Console.WriteLine("Language directory found: {0}", subDirectory.FullName);
        languageDirectories.Add(subDirectory.Parent.Name, subDirectory);
        langDirFound = true;
      }

      // wenn kein langdir gefunden wurde getlangdirs für alle subdirs
      if (!langDirFound)
        foreach (DirectoryInfo subDirectory in subDirectories)
          SearchLangDirs(subDirectory);
    }

    private static void LoadTxProjectFile(string s)
    {
      transifexIni.Load(s);
    }

    private static void TransformMP2toAndroid()
    {
      foreach (KeyValuePair<string, DirectoryInfo> pair in languageDirectories)
      {
        string outputDir = TransifexCache() + "\\" + pair.Key;

        if (!Directory.Exists(outputDir))
          Directory.CreateDirectory(outputDir);

        XslTransform myXslTransform;
        myXslTransform = new XslTransform();
        myXslTransform.Load(XsltMP2toAndroid());
        myXslTransform.Transform(pair.Value.FullName + @"\strings_en.xml", outputDir + @"\strings_en.xml"); 
      }
    }

    private static void UpdateTransifexConfig()
    {
      foreach (KeyValuePair<string, DirectoryInfo> pair in languageDirectories)
      {
        //[MP2.SlimTvClient]
        string section = ProjectSlug + "." + pair.Key;

        //file_filter = SlimTvClient\strings_<lang>.xml
        transifexIni.AddSection(section).AddKey("file_filter").Value = CacheDir + "\\" + pair.Key + @"\strings_<lang>.xml";
        //source_file = SlimTvClient\strings_en.xml
        transifexIni.AddSection(section).AddKey("source_file").Value = CacheDir + "\\" + pair.Key + @"\strings_en.xml";
        //source_lang = en
        transifexIni.AddSection(section).AddKey("source_lang").Value = "en";
        //type = ANDROID
        transifexIni.AddSection(section).AddKey("type").Value = "ANDROID";
      }

      transifexIni.Save(TransifexConfig());
    }

    private static void TransformAndroidToMP2()
    {
      foreach (KeyValuePair<string, DirectoryInfo> pair in languageDirectories)
      {
        string inputDir = TransifexCache() + "\\" + pair.Key;

        foreach (FileInfo file in new DirectoryInfo(inputDir).GetFiles())
        {
          XslTransform myXslTransform;
          myXslTransform = new XslTransform();
          myXslTransform.Load(XsltAndroidtoMP2());
          myXslTransform.Transform(file.FullName, pair.Value.FullName + "\\" + file.Name);
        }
      }
    }
  }
}
