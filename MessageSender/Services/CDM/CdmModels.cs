using System.Collections.Generic;

namespace MessageSender.Services.CDM;

/// <summary>Root object for any cluster JSON file under Clusters/.</summary>
public class CdmCluster
{
    public int ID { get; set; }
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, CdmField> Attributes { get; set; } = new();
    public Dictionary<string, CdmCommand> Commands { get; set; } = new();
    public Dictionary<string, CdmStruct> Structs { get; set; } = new();
    public Dictionary<string, CdmEnumDef> Enums { get; set; } = new();
    public Dictionary<string, CdmField> Events { get; set; } = new();
}

/// <summary>A single attribute or command field definition.</summary>
public class CdmField
{
    public int ID { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int? StructID { get; set; }
    public int? EnumID { get; set; }
    public bool IsArray { get; set; }
}

/// <summary>A command definition – holds a Request field dictionary.</summary>
public class CdmCommand
{
    public int ID { get; set; }
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, CdmField> Request { get; set; } = new();
}

/// <summary>A struct definition – holds an Attributes field dictionary.</summary>
public class CdmStruct
{
    public int ID { get; set; }
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, CdmField> Attributes { get; set; } = new();
}

/// <summary>An enum definition with its list of named values.</summary>
public class CdmEnumDef
{
    public int ID { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<CdmEnumItem> Items { get; set; } = new();
}

/// <summary>A single named enum value.</summary>
public class CdmEnumItem
{
    public int Value { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>Root of MsgTypes/CDMMessageStructs.json.</summary>
public class CdmMessageDefinitions
{
    public int ID { get; set; }
    public Dictionary<string, CdmCommand> Commands { get; set; } = new();
    public Dictionary<string, CdmStruct> Structs { get; set; } = new();
    public Dictionary<string, CdmEnumDef> Enums { get; set; } = new();
}
