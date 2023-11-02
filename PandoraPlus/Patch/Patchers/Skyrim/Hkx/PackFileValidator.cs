﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Pandora.Patch.Patchers.Skyrim.Hkx
{
	public class PackFileValidator
	{
		private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

		private static Regex EventFormat = new Regex(@"[$]{1}eventID{1}[\[]{1}(.+)[\]]{1}[$]{1}");
		private static Regex VarFormat = new Regex(@"[$]{1}variableID{1}[\[]{1}(.+)[\]]{1}[$]{1}");

		private Dictionary<string, int> eventIndices = new Dictionary<string, int>();
		private Dictionary<string, int> variableIndices = new Dictionary<string, int>();


		private int GetIndexFromMatch(Dictionary<string, int> map, Match match)
		{
			if (!match.Success) return -1;

			int index;
			if (!map.TryGetValue(match.Groups[1].Value, out index)) return -1;

			return index;
		}
		private bool ValidateEventsAndVariables(PackFile packFile)
		{
			XElement? stringDataContainer = packFile.GetNodeByClass("hkbBehaviorGraphStringData");
			if (stringDataContainer == null) return false;

			XElement? variableValueSetContainer = packFile.GetNodeByClass("hkbVariableValueSet");
			if (variableValueSetContainer == null) return false;

			XElement? graphDataContainer = packFile.GetNodeByClass("hkbBehaviorGraphData");
			if (graphDataContainer == null) return false;	

			XElement? eventNameContainer = stringDataContainer.Elements().FirstOrDefault();
			if (eventNameContainer == null) return false;

			XElement eventFlagContainer = graphDataContainer.Elements().ElementAt(3);

			XElement variableNameContainer = stringDataContainer.Elements().ElementAt(2);
			XElement variableValueContainer = variableValueSetContainer.Elements().FirstOrDefault()!;
			XElement variableTypeContainer = graphDataContainer.Elements().ElementAt(1);

			var eventNameElements = eventNameContainer.Elements().ToList();
			var eventFlagElements = eventFlagContainer.Elements().ToList();

			var variableNameElements = variableNameContainer.Elements().ToList();
			var variableValueElements = variableValueContainer.Elements().ToList();
			var variableTypeElements = variableTypeContainer.Elements().ToList();

			var uniqueEventNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var uniqueVariableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			eventIndices.Clear();
			variableIndices.Clear();

			for (int i = eventNameElements.Count - 1; i >= 0; i--)
			{
				var eventNameElement = eventNameElements[i];
				var eventName = eventNameElement.Value;
				if (!uniqueEventNames.Add(eventName))
				{
					eventNameElement.Remove();
					eventFlagElements[i].Remove();

					eventNameElements.RemoveAt(i);
					eventFlagElements.RemoveAt(i);
					Logger.Warn($"Validator > {packFile.ParentProject?.Identifier}~{packFile.Name} > Duplicate Event > {eventName} > REMOVED");
					continue;
				}
//#if DEBUG
//				Logger.Debug($"Validator > {packFile.ParentProject?.Identifier}~{packFile.Name} > Mapped Event > {eventName} > Index {i}");
//#endif
				
			}
			for (int i = variableNameElements.Count - 1; i >= 0; i--)
			{
				var variableNameElement = variableNameElements[i];	
				var variableName = variableNameElement.Value;
				if (!uniqueVariableNames.Add(variableName))
				{
					variableNameElement.Remove();
					variableTypeElements[i].Remove();
					variableValueElements[i].Remove();

					variableNameElements.RemoveAt(i);
					variableTypeElements.RemoveAt(i);
					variableValueElements.RemoveAt(i);
					Logger.Warn($"Validator > {packFile.ParentProject?.Identifier}~{packFile.Name} > Duplicate Variable > {variableName} > REMOVED");
					continue; 
				}
//#if DEBUG
//				Logger.Debug($"Validator > {packFile.ParentProject?.Identifier}~{packFile.Name} > Mapped Variable > {variableName} > Index {i}");
//#endif
				
			}
			for (int i = 0; i < eventNameElements.Count; i++)
			{
				eventIndices.Add(eventNameElements[i].Value, i);
			}
			for (int i = 0; i < variableNameElements.Count; i++)
			{
				variableIndices.Add(variableNameElements[i].Value, i);
			}
			return true; 
		}
		private void ValidateElementText(XElement element, Dictionary<string, int> eventIndices, Dictionary<string, int> variableIndices)
		{
			string rawValue = element.Value;

			var eventMatch = EventFormat.Matches(rawValue);
			foreach (Match match in eventMatch)
			{
				var index = GetIndexFromMatch(eventIndices, match);
				
				rawValue = rawValue.Replace(match.Value, index.ToString());
			}

			var varMatch = VarFormat.Matches(element.Value);
			foreach (Match match in varMatch)
			{
				var index = GetIndexFromMatch(eventIndices, match);
				rawValue = rawValue.Replace(match.Value, index.ToString());
			}
			element.SetValue(rawValue);
		}

		private void ValidateElementContent(XElement element, Dictionary<string, int> eventIndices, Dictionary<string, int> variableIndices)
		{

			if (!element.HasElements)
			{
				ValidateElementText(element, eventIndices, variableIndices);
				return;
			}


			foreach (var xelement in element.Elements())
			{
				
				ValidateElementContent(xelement, eventIndices, variableIndices);
			}

		}

		public void TryValidateClipGenerator(string path, PackFile packFile)
		{
			XElement element;
			if (!packFile.Map.TryLookup($"{path}/animationName", out element)) return;
			string clipName = packFile.Map.Lookup($"{path}/name").Value!;
			packFile.ParentProject?.AnimData?.AddDummyClipData(clipName);
		}

		public void Validate(PackFile packFile, params List<IPackFileChange>[] changeLists)
		{
			if (!ValidateEventsAndVariables(packFile)) return; 
			int changeCount = 0;
			foreach(var changeSet in changeLists)
			{
				foreach(IPackFileChange change in changeSet)
				{
					XElement element;
					if (!packFile.Map.TryLookup(change.Path, out element))
					{
						continue;
					}
					//ValidateElementCount(element.Parent!); might not be needed with hkx2 library; testing needed.
					ValidateElementContent(element, eventIndices, variableIndices);
					changeCount++;
				}
			}
			Logger.Info($"Validator > {packFile.ParentProject?.Identifier}~{packFile.Name} > {changeCount} Edits > CHECKED");
		}
	}
}
