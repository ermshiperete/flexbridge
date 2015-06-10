// --------------------------------------------------------------------------------------------
// Copyright (C) 2010-2013 SIL International. All rights reserved.
//
// Distributable under the terms of the MIT License, as specified in the license.rtf file.
// --------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using LibFLExBridgeChorusPlugin.Infrastructure;
using LibFLExBridgeChorusPlugin.DomainServices;
using LibFLExBridgeChorusPlugin;

namespace LibFLExBridgeChorusPlugin.Contexts.Linguistics.Phonology
{
	internal static class PhonologyBoundedContextService
	{
		internal static void NestContext(string linguisticsBaseDir,
			IDictionary<string, XElement> wellUsedElements,
			IDictionary<string, SortedDictionary<string, byte[]>> classData,
			Dictionary<string, string> guidToClassMapping)
		{
			var phonologyDir = Path.Combine(linguisticsBaseDir, SharedConstants.Phonology);
			if (!Directory.Exists(phonologyDir))
				Directory.CreateDirectory(phonologyDir);

			var langProjElement = wellUsedElements[SharedConstants.LangProject];

			// 1. Nest: LP's PhonologicalData(PhPhonData OA) (Also does PhPhonData's PhonRuleFeats(CmPossibilityList)
			// NB: PhPhonData is a singleton
			var phonDataPropElement = langProjElement.Element("PhonologicalData");
			var phonDataElement = Utilities.CreateFromBytes(classData["PhPhonData"].Values.First());
			// 1.A. Write: Break out PhPhonData's PhonRuleFeats(CmPossibilityList OA) and write in its own .list file. (If it exists, but *before* nesting "PhPhonData".)
			FileWriterService.WriteNestedListFileIfItExists(
				classData, guidToClassMapping,
				phonDataElement, "PhonRuleFeats",
				Path.Combine(phonologyDir, SharedConstants.PhonRuleFeaturesFilename));
			phonDataPropElement.RemoveNodes();
			CmObjectNestingService.NestObject(
				false,
				phonDataElement,
				classData,
				guidToClassMapping);
			// 2. Nest: LP's PhFeatureSystem(FsFeatureSystem OA)
			var phonFeatureSystemPropElement = langProjElement.Element("PhFeatureSystem");
			var phonFeatureSystemElement = Utilities.CreateFromBytes(classData["FsFeatureSystem"][phonFeatureSystemPropElement.Element(SharedConstants.Objsur).Attribute(SharedConstants.GuidStr).Value]);
			phonFeatureSystemPropElement.RemoveNodes();
			CmObjectNestingService.NestObject(
				false,
				phonFeatureSystemElement,
				classData,
				guidToClassMapping);
			// B. Write: LP's PhonologicalData(PhPhonData) (Sans its PhonRuleFeats(CmPossibilityList) in a new extension (phondata).
			FileWriterService.WriteNestedFile(Path.Combine(phonologyDir, SharedConstants.PhonologicalDataFilename), new XElement("PhonologicalData", phonDataElement));
			// C. Write: LP's PhFeatureSystem(FsFeatureSystem) in its own file with a new (shared extension of featsys).
			FileWriterService.WriteNestedFile(Path.Combine(phonologyDir, SharedConstants.PhonologyFeaturesFilename), new XElement(SharedConstants.FeatureSystem, phonFeatureSystemElement));
		}

		internal static void FlattenContext(
			SortedDictionary<string, XElement> highLevelData,
			SortedDictionary<string, XElement> sortedData,
			string linguisticsBaseDir)
		{
			var phonologyDir = Path.Combine(linguisticsBaseDir, SharedConstants.Phonology);
			if (!Directory.Exists(phonologyDir))
				return;

			var langProjElement = highLevelData[SharedConstants.LangProject];
			var currentPathname = Path.Combine(phonologyDir, SharedConstants.PhonologyFeaturesFilename);
			if (File.Exists(currentPathname))
			{
				var phoneFeatSysDoc = XDocument.Load(currentPathname);
				BaseDomainServices.RestoreElement(
					currentPathname,
					sortedData,
					langProjElement, "PhFeatureSystem",
					phoneFeatSysDoc.Root.Element("FsFeatureSystem")); // Owned elment.
			}

			currentPathname = Path.Combine(phonologyDir, SharedConstants.PhonologicalDataFilename);
			if (!File.Exists(currentPathname))
				return;

			var phonDataDoc = XDocument.Load(currentPathname);
			var phonDataElement = phonDataDoc.Root.Element("PhPhonData");
			BaseDomainServices.RestoreElement(
				currentPathname,
				sortedData,
				langProjElement, "PhonologicalData",
				phonDataElement); // Owned elment.

			// Optional PhonRuleFeats list.
			currentPathname = Path.Combine(phonologyDir, SharedConstants.PhonRuleFeaturesFilename);
			if (!File.Exists(currentPathname))
				return;

			var phonRuleFeatsListDoc = XDocument.Load(currentPathname);
			BaseDomainServices.RestoreElement(
				currentPathname,
				sortedData,
				phonDataElement, "PhonRuleFeats",
				phonRuleFeatsListDoc.Root.Element(SharedConstants.CmPossibilityList)); // Owned elment.
		}
	}
}