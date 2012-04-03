﻿using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using FLEx_ChorusPlugin.Contexts;

namespace FLEx_ChorusPlugin.Infrastructure.DomainServices
{
	/// <summary>
	/// Service that will manage the multiple files and original fwdata file for a full FW data set.
	///
	/// The task of the service is twofold:
	/// 1. Break up the main fwdata file into multiple files
	///		A. One file for the custom property declarations (even if there are no custom properties), and
	///		B. One file for the model version
	///		C. Various files for the CmObject data.
	/// 2. Put the multiple files back together into the main fwdata file,
	///		but only if a Send/Receive had new information brought back into the local repo.
	///		NB: The client of the service decides if new information was found, and decides to call the service, or not.
	/// </summary>
	internal static class MultipleFileServices
	{
		internal static void PutHumptyTogetherAgain(string mainFilePathname)
		{
			FileWriterService.CheckPathname(mainFilePathname);

			var pathRoot = Path.GetDirectoryName(mainFilePathname);
			var tempPathname = Path.GetTempFileName();

			try
			{
				// There is no particular reason to ensure the order of objects in 'mainFilePathname' is retained,
				// but the optional custom props element must be first.
				// NB: This should follow current FW write settings practice.
				var fwWriterSettings = new XmlWriterSettings
				{
					OmitXmlDeclaration = false,
					CheckCharacters = true,
					ConformanceLevel = ConformanceLevel.Document,
					Encoding = new UTF8Encoding(false),
					Indent = true,
					IndentChars = (""),
					NewLineOnAttributes = false
				};

				using (var writer = XmlWriter.Create(tempPathname, fwWriterSettings))
				{
					writer.WriteStartElement("languageproject");

					// Write out version number from the ModelVersion file.
					var modelVersionData = File.ReadAllText(Path.Combine(pathRoot, SharedConstants.ModelVersionFilename));
					var splitModelVersionData = modelVersionData.Split(new[] { "{", ":", "}" }, StringSplitOptions.RemoveEmptyEntries);
					var version = splitModelVersionData[1].Trim();
					writer.WriteAttributeString("version", version);

					var mdc = MetadataCache.MdCache; // This may really need to be a reset
					mdc.UpgradeToVersion(Int32.Parse(version));

					// Write out optional custom property data to the fwdata file.
					// The foo.CustomProperties file will exist, even if it has nothing in it, but the "AdditionalFields" root element.
					var optionalCustomPropFile = Path.Combine(pathRoot, SharedConstants.CustomPropertiesFilename);
					// Remove 'key' attribute from CustomField elements, before writing to main file.
					var doc = XDocument.Load(optionalCustomPropFile);
					var customFieldElements = doc.Root.Elements("CustomField").ToList();
					if (customFieldElements.Any())
					{
						foreach (var cf in customFieldElements)
						{
							cf.Attribute("key").Remove();
							// Restore type attr for object values.
							var propType = cf.Attribute("type").Value;
							cf.Attribute("type").Value = RestoreAdjustedTypeValue(propType);

							mdc.GetClassInfo(cf.Attribute(SharedConstants.Class).Value).AddProperty(new FdoPropertyInfo(cf.Attribute(SharedConstants.Name).Value, propType, true));
						}
						mdc.ResetCaches();
						FileWriterService.WriteElement(writer, SharedConstants.Utf8.GetBytes(doc.Root.ToString()));
					}

					BaseDomainServices.RestoreDomainData(writer, pathRoot);

					writer.WriteEndElement();
				}

				File.Copy(tempPathname, mainFilePathname, true);
			}
			finally
			{
				if (File.Exists(tempPathname))
					File.Delete(tempPathname);
			}
		}

		private static string RestoreAdjustedTypeValue(string storedType)
		{
			string adjustedType;
			switch (storedType)
			{
				default:
					adjustedType = storedType;
					break;

				case "OwningCollection":
					adjustedType = "OC";
					break;
				case "ReferenceCollection":
					adjustedType = "RC";
					break;

				case "OwningSequence":
					adjustedType = "OS";
					break;
				case "ReferenceSequence":
					adjustedType = "RS";
					break;

				case "OwningAtomic":
					adjustedType = "OA";
					break;
				case "ReferenceAtomic":
					adjustedType = "RA";
					break;
			}
			return adjustedType;
		}
	}
}
