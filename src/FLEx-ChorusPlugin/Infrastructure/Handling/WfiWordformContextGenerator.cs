﻿using System.Xml;
using FLEx_ChorusPlugin.Properties;

namespace FLEx_ChorusPlugin.Infrastructure.Handling
{
	/// <summary>
	/// Context generator for LexEntry elements. These are a root element, so we generate a label directly,
	/// without needing to look further up the chain.
	/// </summary>
	class WfiWordformContextGenerator : FieldWorkObjectContextGenerator
	{
		protected override string GetLabel(System.Xml.XmlNode start)
		{
			return GetLabelForWordform(start);
		}


		string WordformLabel
		{
			get { return Resources.kWfiWordFormClassLabel; }
		}

		private string GetLabelForWordform(XmlNode wordform)
		{
			var form = wordform.SelectSingleNode("Form/AUni");
			if (form == null)
				return WordformLabel;
			return WordformLabel + " " + form.InnerText;
		}
	}
}