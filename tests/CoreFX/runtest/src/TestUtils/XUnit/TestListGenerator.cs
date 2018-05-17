using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml;
using Newtonsoft.Json;

namespace CoreFX.TestUtils.XUnit
{
    /// <summary>
    /// The only purpose of this class is to generate a JSON testlist which will ignore all failing tests
    /// </summary>
    public class TestListGenerator
    {

        public HashSet<string> methodNames;

        public TestListGenerator()
        {
            methodNames = new HashSet<string>();
        }

        public void GenerateFromXML(IEnumerable<string> logFiles, string outputPath)
        {
            Debug.Assert(Directory.Exists(outputPath));
            GenerateJSONTestListAssembly(ReadXML(logFiles), outputPath);
        }

        public void GenerateJSONTestListAssembly(List<XUnitTestAssembly> assemblies, string outputPath)
        {
            Debug.Assert(Directory.Exists(outputPath));
            string outputFile = Path.Combine(outputPath, "TopN.CoreFX.issues.json");
            JsonSerializer jsonSerializer = new JsonSerializer();
            using (StreamWriter stream = new StreamWriter(outputFile))
            using (JsonWriter wr = new JsonTextWriter(stream))
            {
                jsonSerializer.Serialize(wr, assemblies.ToArray());
            }
        }

        public List<XUnitTestAssembly> ReadXML(IEnumerable<string> logFiles)
        {
            var parsedAssemblies = new List<XUnitTestAssembly>();

            foreach (string logFile in logFiles)
            {
                try
                {
                    // XMLReader escapes the character sequence \\.. as just a single backslash \ - Is this intended behavior? 
                    using (XmlReader collectionReader = XmlReader.Create(logFile.Replace(@"\\..", @"\..")))
                    {

                        XUnitTestAssembly assembly = new XUnitTestAssembly();
                        assembly.Name = Path.GetFileNameWithoutExtension(logFile);

                        collectionReader.MoveToContent();
                        collectionReader.ReadToFollowing("assembly");
                        int failedTests = 0;
                        Int32.TryParse(collectionReader.GetAttribute("failed"), out failedTests);

                        assembly.Exclusions = new Exclusions();
                        List<Exclusion> exclusions = new List<Exclusion>();
                        collectionReader.ReadToFollowing("collection");

                        do
                        {
                            using (XmlReader testReader = collectionReader.ReadSubtree())
                            {
                                testReader.ReadToDescendant("test");
                                do
                                {
                                    string result = testReader.GetAttribute("result");
                                    if (result == "Fail")
                                    {
                                        string testName = testReader.GetAttribute("name");
                                        string sanitizedTestName = testName.Substring(0, testName.IndexOf('(') < 0 ? testName.Length : testName.IndexOf('('));
                                        if (methodNames.Contains(sanitizedTestName))
                                            continue;

                                        methodNames.Add(sanitizedTestName);
                                        testReader.ReadToDescendant("failure");
                                        string failureReason = testReader.GetAttribute("exception-type");
                                        exclusions.Add(new Exclusion() { Name = sanitizedTestName, Reason= failureReason });
                                    }
                                }
                                while (testReader.ReadToFollowing("test"));
                            }

                        } while (collectionReader.ReadToNextSibling("collection"));

                        assembly.Exclusions.Methods = exclusions.ToArray();

                        parsedAssemblies.Add(assembly);
                    }
                }
                catch (XmlException exc)
                {
                    Console.WriteLine("Malformed Log: {0} ", logFile);
                    Console.WriteLine("Reason: {0} ", exc.Message);
                    continue;
                }
            }
            foreach(var assembly in parsedAssemblies)
            {
                foreach(var exclusion in assembly.Exclusions.Methods)
                {
                    Console.WriteLine(exclusion.Name + " " + exclusion.Reason);
                }
            }
            return parsedAssemblies;
        }
    }
}
