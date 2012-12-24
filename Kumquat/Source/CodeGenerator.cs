﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Lime;

namespace Kumquat
{

	public class CodeGenerator
	{
		private string Directory;
		private string ProjectName;
		private Dictionary<string, Frame> Locations;
		List<string> lines = new List<string>();

		public CodeGenerator(string directory, string projectName, Dictionary<string, Frame> locations)
		{
			Directory = directory;
			ProjectName = projectName;
			Locations = locations;
		}

		private delegate void BodyDelegate();

		public void Start()
		{
			var sourcePath = String.Format(@"{0}\{1}.Game\Source\", Directory, ProjectName);
			var generatedPath = sourcePath + "Generated";
			var locationsPath = sourcePath + "Locations";

			AddLocationCollectionHeader();
			foreach (string locPath in Locations.Keys) {
				var name = Path.GetFileNameWithoutExtension(locPath);
				var line = "\t\tpublic NAME NAME { get { return (NAME)Dictionary[\"NAME\"]; } }";
				lines.Add(line.Replace("NAME", name));
			}
			AddFooter();
			System.IO.File.WriteAllLines(generatedPath + "\\LocationCollection.cs", lines);

			foreach (var loc in Locations) {
				var className = Path.GetFileNameWithoutExtension(loc.Key);
				var path = locationsPath + "\\" + className + ".cs";
				if (!File.Exists(path)) {
					lines.Clear();
					AddLocationTemplate(className);
					System.IO.File.WriteAllLines(path, lines);
				}

				lines.Clear();
				AddLocationHeader(className);
				var line = "\t\tpublic Item @NAME { get { return Items[\"NAME\"]; } }";
				HashSet<string> names = new HashSet<string>();
				foreach (Area area in loc.Value.Descendants<Area>()) {
					if (!names.Contains(area.Id)) {
						names.Add(area.Id);
						lines.Add(line.Replace("NAME", area.Id));
					} else {
						Console.WriteLine("WARNING: Duplicate '{0}' on '{1}'", area.Id, className);
					}
				}
				AddFooter();
				System.IO.File.WriteAllLines(generatedPath + "\\" + className + ".cs", lines);
			}

		}

		private void AddLocationCollectionHeader()
		{
			var header = @"
using PROJECT_NAME.Locations;

namespace PROJECT_NAME
{
	public partial class LocationCollection
	{";
			lines.Add(header.Replace("PROJECT_NAME", ProjectName));
		}

		private void AddLocationHeader(string className)
		{
			var header = @"
using Kumquat;
using ProtoBuf;

namespace PROJECT_NAME.Locations
{
	[ProtoContract]
	public partial class CLASS_NAME : Location
	{";
			header = header.Replace("PROJECT_NAME", ProjectName);
			header = header.Replace("CLASS_NAME", className);
			lines.Add(header);
		}

		private void AddLocationTemplate(string className)
		{
			var text = @"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Lime;
using Kumquat;

namespace PROJECT_NAME.Locations
{
	using R = IEnumerator<object>;

	public partial class CLASS_NAME : Location
	{
	}
}";
			text = text.Replace("PROJECT_NAME", ProjectName);
			text = text.Replace("CLASS_NAME", className);
			lines.Add(text);
		}

		private void AddFooter()
		{
			lines.Add("\t}");
			lines.Add("}");
		}

	}

}
