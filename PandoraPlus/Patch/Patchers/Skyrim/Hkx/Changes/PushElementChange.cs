﻿using Pandora.Core;
using System;
using System.Xml;
using System.Xml.Linq;

namespace Pandora.Patch.Patchers.Skyrim.Hkx;

public class PushElementChange : IPackFileChange
{
	public IPackFileChange.ChangeType Type { get; } = IPackFileChange.ChangeType.Insert;

	public XmlNodeType AssociatedType { get; } = XmlNodeType.Element;

	public string Path { get; private set; }

	private XElement element { get; set; }



	public PushElementChange(string path, XElement element)
	{
		Path = path;
		this.element = element;
	}
	public bool Apply(PackFile packFile)
	{

		if (!packFile.Map.PathExists(Path)) return false;
		string newPath = PackFileEditor.PushElement(packFile, Path, element);
		Path = String.IsNullOrEmpty(newPath) ? Path : newPath;
		return packFile.Map.PathExists(Path);
	}

	public bool Revert(PackFile packFile)
	{
		if (!packFile.Map.PathExists(Path)) return false;
		PackFileEditor.RemoveElement(packFile, Path);
		return true;
	}
}
