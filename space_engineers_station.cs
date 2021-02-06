const string ASSEMB_NAME = "AssemblerAutomatic";
const string TIMER = "P_ResourceDisplay_LoopTimer";
const string DEBUG_NAME = "P_DebugDisplay";
const string DEBUG2_NAME = "P_ResourceDisplay_Debug2";
const string SELF = "P_ResourceDisplay_ProgBlock";

const string LCD = "P_ResourceDisplay_Display";
const string LCD_EX_Ammo = "A";
const string LCD_EX_Components = "C";
const string LCD_EX_Ingots = "I";
const string LCD_EX_Ores = "O";
const string LCD_EX_Gases = "G";

const int MAX_LINE_COUNT = 26;
const int COLS = 2;
const int LINE_SIZE = (MAX_LINE_COUNT + 8) * COLS;

const int MAX_TEXT = 15; //max len for displaying item names
const int MAX_NUM = 7;

const int GAS_LCD_WIDTH = 23;

const String MULTIPLIERS = " kMGTPEZY";

// Dictionaries are below so you can jump right into code ;)

public void Main(string argument, UpdateType updateSource)
{
    ClearDebug();
    System.DateTime now = System.DateTime.UtcNow;
    WriteDebug("-----");
    WriteDebug("Space_Engineers_Station script\n" + now.ToString());

    TriggerTimer();

    //Show some text
    IMyTextPanel panel = (IMyTextPanel)GridTerminalSystem.GetBlockWithName(LCD);
    if (panel != null) { WriteDebug(LCD + " found."); }

    WriteDebug("Getting resources...");
    Dictionary<string, VRage.MyFixedPoint> resources = GetResources();
    WriteDebug("Displaying resources...");
    DisplayResourceTargets(resources, LCD, "Resource status", null);
    DisplayResourceTargets(resources, LCD + LCD_EX_Ingots, "Ingots", ingotNames);
    DisplayResourceTargets(resources, LCD + LCD_EX_Components, "Components", cNames);
    DisplayResourceTargets(resources, LCD + LCD_EX_Ores, "Ores", oreNames);

    DisplayGases(resources, LCD + LCD_EX_Gases);

    var assembler = GridTerminalSystem.GetBlockWithName(ASSEMB_NAME) as IMyAssembler;
    if (assembler != null)
    {
        WriteDebug(ASSEMB_NAME + " found.");
        Assemble(assembler, resources);
    }

    WriteDebug("-----");
}

private void TriggerTimer()
{
    var timer = (GridTerminalSystem.GetBlockWithName(TIMER) as IMyTimerBlock);
    if (timer != null) { WriteDebug(TIMER + " found."); } else { WriteDebug("No timer found."); }
    var action = timer.GetActionWithName("Start");
    action.Apply(timer);
}

private void Assemble(IMyAssembler assembler, Dictionary<string, VRage.MyFixedPoint> resources)
{
    if (!assembler.IsProducing && assembler.IsQueueEmpty)
    {
        foreach (var target in targetItemAmount)
        {
            if (resources[target.Key] < target.Value)
            {
                var neededAmount = target.Value - resources[target.Key];
                var percentage = resources[target.Key] * (1f/target.Value);
  
                VRage.MyFixedPoint neededRes = 1;

                // a production step is 15% when below 20%, 5% when below 80%, 1% when above
                if(percentage < (MyFixedPoint)0.2)
                {
                    neededRes *= target.Value * 0.15f;
                }
                else if(percentage < (MyFixedPoint)0.8)
                {
                    neededRes *= target.Value * 0.05f;
                }
                else
                {
                    neededRes *= target.Value * 0.01f;
                }

                neededRes = MyFixedPoint.Ceiling(neededRes);
                WriteDebug(target.Key + " " + neededRes.ToString());

                if (assemblerNames.ContainsKey(target.Key))
                {
                    MyDefinitionId component = new MyDefinitionId();
                    if (MyDefinitionId.TryParse(assemblerNames[target.Key], out component))
                        assembler.AddQueueItem(component, neededRes);
                }
            }
        }
    }
}

private void DisplayGases(Dictionary<string, VRage.MyFixedPoint> resources, string targetDisplayName)
{
    List<IMyTerminalBlock> panels = new List<IMyTerminalBlock>();
    GridTerminalSystem.SearchBlocksOfName(targetDisplayName+".", panels);

    WriteDebug(panels.Count.ToString()+" gas panel found.");

    if (!panels.Any())
    {
        return;
    }

    VRage.MyFixedPoint amountIce = 0;

        if(resources.ContainsKey("OIce"))
        {
            amountIce = resources["OIce"];
        }

        List<IMyGasTank> oxy = new List<IMyGasTank>();
        GridTerminalSystem.GetBlocksOfType<IMyGasTank>(oxy);

        var oxCapacity = 0.0d;
        var oxFilled = 0.0d;

        var hydCapacity = 0.0d;
        var hydFilled = 0.0d;

        foreach(var container in oxy)
        {
            if(!container.IsFunctional)
            {
                continue;
            }

            if(container.BlockDefinition.SubtypeId.Contains("Hydro"))
            {
                hydCapacity += container.Capacity;
                hydFilled += container.Capacity * container.FilledRatio;
            }
            else
            {
                oxCapacity += container.Capacity;
                oxFilled += container.Capacity * container.FilledRatio;
            }
        }

    foreach (IMyTextPanel panel in panels)
    {
        if (panel.GetPublicTitle() == "c")
        {
            panel.ContentType = ContentType.TEXT_AND_IMAGE;
            panel.FontSize = 1;
            panel.Font = "Monospace";
            panel.WritePublicTitle(".", false);
        }

        panel.WriteText("Ice     :        " + Format((double)amountIce)+"\n\n\n\n");
        string perOx = PercentageBar(oxFilled/oxCapacity, GAS_LCD_WIDTH) + "\n";
        panel.WriteText(perOx+perOx+perOx, true);
        panel.WriteText("Oxygen  : " + Format(oxFilled) + "/" + Format(oxCapacity) + "\n\n\n\n", true);
        string perHyd = PercentageBar(hydFilled/hydCapacity, GAS_LCD_WIDTH) + "\n";
        panel.WriteText(perHyd+perHyd+perHyd, true);
        panel.WriteText("Hydrogen: " + Format(hydFilled) + "/" + Format(hydCapacity)+"\n", true);
    }
}

private string PercentageBar(double percentage, int width)
{
    return "[" + new String('#', (int)(width * percentage)) + new String('_', (int)(width * (1-percentage))) + "]";
}

private void DisplayResourceTargets(Dictionary<string, VRage.MyFixedPoint> resources, string targetDisplayName, string header, List<string> allowedResDisplay)
{
    List<IMyTerminalBlock> panels = new List<IMyTerminalBlock>();
    GridTerminalSystem.SearchBlocksOfName(targetDisplayName+".", panels);

    if (!panels.Any())
    {
        return;
    }

    

    var list = resources.Keys.ToList();
    list.Sort();

    var maxText = MAX_TEXT;

    if (allowedResDisplay != null)
    {
        maxText = Math.Min(allowedResDisplay.Max(x => x.Count()) + 1, MAX_TEXT);
    }

    var rs = new ResourceData(maxText);

    foreach (var litem in list)
    {
        if (allowedResDisplay != null)
        {
            if (!allowedResDisplay.Contains(litem))
                continue;
        }

        string s = string.Empty;

        int a = (int)(double)resources[litem];

        Color col = Color.White;
        if (targetItemAmount.ContainsKey(litem))
        {
            int ta = targetItemAmount[litem];

            if (ta > 0)
            {
                var percent = a * 100 / ta;
                if (percent >= 100)
                {
                    percent = 100;
                }
                else
                {
                    col = Color.Red;
                }

                rs.Add(Fill(litem.ToString(), maxText), Fill(Format(a), MAX_NUM, ' '), "(" + percent.ToString() + "%)", col);
            }
            else
            {
                rs.Add(Fill(litem.ToString(), maxText), Fill(Format(a), MAX_NUM, ' '), "", col);
            }
        }
        else
        {
            rs.Add(Fill(litem.ToString(), maxText), Fill(Format(a), MAX_NUM, ' '), "", col);
            var debug = GridTerminalSystem.GetBlockWithName(DEBUG2_NAME) as IMyTextPanel;
            if (debug != null) { debug.WriteText(s + "\n", true); }
        }
    }

    foreach (IMyTextPanel panel in panels.Where(p => p.CustomName.StartsWith(targetDisplayName)))
    {
        if (panel.GetPublicTitle() == "c")
        {
            panel.ContentType = ContentType.TEXT_AND_IMAGE;
            panel.FontSize = 1;
            panel.Font = "Monospace";
            panel.WritePublicTitle(".", false);
        }

        if(true)//!panel.CustomName.EndsWith("No100"))
        {
            panel.WriteText(header + "\n");
            panel.WriteText(new String('-', maxText + 14) + "\n", true);

            if(panel.CustomName.Contains("Names"))
            {
                rs.OutJustNames(panel, panel.CustomName.Contains("No100"));
                continue;
            }

            rs.Out(panel, panel.CustomName.Contains("No100"));
        }
    }

    //panel.SetValue<Color>( "FontColor", Color.Red ); // set preset font colour 
    //panel.WriteText(string.Join("\n", output) + "\n", true);
}

private void DisplayResources(Dictionary<string, VRage.MyFixedPoint> resources)
{
    IMyTextPanel panel = (IMyTextPanel)GridTerminalSystem.GetBlockWithName(LCD);

    var list = resources.Keys.ToList();
    list.Sort();

    for (int i = 0; i < list.Count; i++)
    {
        var litem = list[i];

        string s = litem.ToString() + " " + resources[litem];

        int space = ((LINE_SIZE - 3) / COLS) - s.Length;

        if (space > 0)
            s += new String(' ', space);

        panel.WriteText(s, true);

        if (i % COLS == 0)
            panel.WriteText("\n", true);
        else
            panel.WriteText("   ", true);
    }
}

private Dictionary<string, VRage.MyFixedPoint> GetResources()
{
    var result = new Dictionary<string, VRage.MyFixedPoint>();

    foreach (var target in targetItemAmount)
    {
        result[target.Key] = 0;
    }

    var inventoryItems = GetAllBlocks();

    for (int i = 0; i < inventoryItems.Count; i++)
    {
        int inventoryCount = inventoryItems[i].InventoryCount;

        for (int k = 0; k < inventoryCount; k++)
        {
            var inventory = inventoryItems[i].GetInventory(k);

            if (inventory != null)
            {
                List<MyInventoryItem> items = new List<MyInventoryItem>();
                inventory.GetItems(items);

                for (int j = 0; j < items.Count; j++)
                {
                    MyInventoryItem item = items[j];
                    string s = string.Join("", item.ToString().Split(' ').Last().Split('_').Skip(1));
                    s = s.Split('/').First().First().ToString() + string.Join("", s.Split('/').Skip(1));

                    AddIfNotExists(result, s, item.Amount);
                }
            }
        }
    }

    return result;
}

private void AddIfNotExists(Dictionary<string, VRage.MyFixedPoint> result, string s, VRage.MyFixedPoint num)
{
    bool found = false;
    foreach (var pair in result)
    {
        if (pair.Key.Equals(s))
        {
            found = true;
            break;
        }
    }

    if (!found)
    {
        result.Add(s, num);
    }
    else
        result[s] += num;
}

private string Fill(string s, int maxLen, char c = ' ')
{
    if (displayNames.ContainsKey(s))
    {
        s = displayNames[s];
    }

    if (maxLen - s.Length > 0)
        return s + new String(c, maxLen - s.Length);
    else if (maxLen == s.Length)
    {
        return s;
    }
    else
    {
        return s.Substring(0, maxLen - 1) + ".";
    }
}

String Format(double val)
{
    int counter = 0;
    while (Math.Abs(val) > 1000.0)
    {
        val = val / 1000;
        counter++;
    }
    string s = Math.Round((double)val, 2).ToString("##0.#") + MULTIPLIERS.Substring(counter, 1);

    s = new String(' ', 6 - s.Length) + s;

    return s;
}

// This function looks through all blocks in the system and returns all inventories within
public List<IMyTerminalBlock> GetAllBlocks()
{
    List<IMyTerminalBlock> inventories = new List<IMyTerminalBlock>();
    GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(inventories);

    return inventories.Where(x => x.IsSameConstructAs(GridTerminalSystem.GetBlockWithName(SELF))).ToList();
}

private IMyTextPanel _debug = null;
public void ClearDebug()
{
    if (_debug == null)
    {
        _debug = GridTerminalSystem.GetBlockWithName(DEBUG_NAME) as IMyTextPanel;
    }

    if (_debug != null)
    {
        _debug.WriteText("");
    }
}

public void WriteDebug(string s)
{
    if (_debug == null)
    {
        _debug = GridTerminalSystem.GetBlockWithName(DEBUG_NAME) as IMyTextPanel;
    }

    if (_debug != null)
    {
        _debug.WriteText(s + "\n", true);
    }
}

//////////////////////////////////////////////////////////////

private class ResourceDataItem
{
    public string Text => Fill(Fill(ItemName, MaxItemNameLen) + " " + AmountFormatted + PercentageFormatted, ((LINE_SIZE - 3) / COLS), ' ');
    public string ItemName;
    public string AmountFormatted;
    public string PercentageFormatted;
    public Color Color;
    public bool Newline;
    public int MaxItemNameLen;

    public ResourceDataItem(string text, bool newLine)
    {
        ItemName = text;
        Newline = newLine;
        Color = Color.White;
    }

    public ResourceDataItem(string text, int maxItemNameLen, string amountFormatted, string percentageFormatted, bool newLine, Color color)
    {
        ItemName = text;
        Newline = newLine;
        Color = color;
        this.AmountFormatted = amountFormatted;
        this.PercentageFormatted = percentageFormatted;
        this.MaxItemNameLen = maxItemNameLen;
    }

    private string Fill(string s, int maxLen, char c = ' ')
    {
        if (maxLen - s.Length > 0)
            return s + new String(c, maxLen - s.Length);
        else if (maxLen == s.Length)
        {
            return s;
        }
        else
        {
            return s.Substring(0, maxLen - 1) + ".";
        }
    }
}

private class ResourceData
{
    List<ResourceDataItem> textItems = new List<ResourceDataItem>();

    public bool HasItems => this.textItems.Count > 0;

    public int ItemNameLen;

    public ResourceData(int itemNameLen)
    {
        this.ItemNameLen = itemNameLen;
    }

    public void Add(string text, string amount, string percentage, Color? color = null)
    {
        if (color == null)
            color = Color.White;

        textItems.Add(new ResourceDataItem(text, this.ItemNameLen, amount, percentage, true, color.Value));
    }

    //MAX_LINE_COUNT

    public void Out(IMyTextPanel panel, bool no100P)
    {
        if (no100P)
        {
            Out(panel, this.textItems.Where(x => x.PercentageFormatted != "(100%)").ToList());
        }
        else
        {
            Out(panel, this.textItems);
        }
    }

/*
    public void OutNo100P(IMyTextPanel panel)
    {
        Out(panel, this.textItems.Where(x => x.PercentageFormatted != "(100%)").ToList());
    }*/

    public void OutJustNames(IMyTextPanel panel, bool no100P)
    {
        if (no100P)
        {
            Out(panel, this.textItems.Where(x => x.PercentageFormatted != "(100%)").Select(x => x.ItemName).ToList());
        }
        else
        {
            Out(panel, this.textItems.Select(x => x.ItemName).ToList());
        }
    }

    public void Out(IMyTextPanel panel, List<ResourceDataItem> textItems)
    {
        Out(panel, textItems.Select(x => x.Text).ToList());
    }

    public void Out(IMyTextPanel panel, List<string> textItems)
    {
         //Add items together
        List<string> lines = new List<string>();

        for (int i = 0; i < textItems.Count; i++)
        {
            int col = i / MAX_LINE_COUNT;
            int row = i % MAX_LINE_COUNT;

            if (lines.Count <= row)
            {
                lines.Add(textItems[i]);
            }
            else
            {
                lines[row] += textItems[i];
            }
        }

        foreach (string line in lines)
        {
            //panel.SetValue<Color>( "FontColor", item.Color ); // set preset font colour
            panel.WriteText(line + "\n", true);
        }
    }
}

//////////////////////////////////////////////////////////////

public Dictionary<string, int> targetItemAmount = new Dictionary<string, int>()
{
    {"CSteelPlate", 100000},       //20 Light Armor * 1000
    {"CInteriorPlate", 5000},     //Steel Plate / 4
    {"CConstruction", 2500},      //Interior / 2
    {"CBulletproofGlass", 1500},  //196 Window 3x3 Flat
    {"CGirder", 350},             //40 Window 3x3 Flat
    {"CComputer", 500},           //300 Jump Drive
    {"CDisplay", 300},            //20 Wide LCD * 15
    {"CExplosives", 18},          //6 Warhead
    {"CSmallTube", 500},          //20 Hydrogen Engine
    {"CLargeTube", 350},          //40 Large Reactor
    {"CMetalGrid", 20000},         //50 Heavy Armor * 200
    {"CMotor", 2500},             //20 Large Reactor
    {"CPowerCell", 600},          //80 Battery
    {"CRadioCommunication", 120}, //40 Beacon * 3
    {"CReactor", 2200},           //2000 Large Reactor
    {"CSolarCell", 128},          //32 Solar Cell * 4
    {"CThrust", 6000},            //960 Large Thruster * 6
    {"CMedical", 15},             //15 Medical Room
    {"CDetector", 50},            //20 Jump Drive
    {"CGravityGenerator", 10},    //20 Jump Drive
    {"CSuperconductor", 1000},    //1000 Jump Drive
                                  
    {"ISilver", 2000},            
    {"IGold", 100},               
    {"IUranium", 20},             
    {"OIce", 5000},               
    {"IPlatinum", 260},           
    {"IStone", 15000},            
    {"ISilicon", 15000},
    {"INickel", 20000},
    {"IIron", 150000},
    {"IMagnesium", 200},
    {"ICobalt", 20000},

    {"GHydrogenBottle", 3},

    {"AMissile200mm", 0},
    {"ANATO25x184mm", 0},
    {"ANATO5p56x45mm", 0},
    {"CCanvas", 0},
    {"OOxygenBottle", 0},
    {"OIron", 0},
    {"ONickel", 0},
    {"OMagnesium", 0},
    {"OScrap", 0},
    {"OSilver", 0},
    {"OSilicon", 0},
    {"OPlatinum", 0},
    {"OUranium", 0},
    {"OGold", 0},
    {"OStone", 0},
    {"OCobalt", 0},
    {"PSpaceCredit", 0},
    {"PAngleGrinderItem", 0},
    {"PAngleGrinder2Item", 0},
    {"PAngleGrinder3Item", 0},
    {"PAngleGrinder4Item", 0},
    {"PWelderItem", 0},
    {"PWelder2Item", 0},
    {"PWelder3Item", 0},
    {"PWelder4Item", 0},
    {"PAutomaticRifleItem", 0},
    {"PHandDrillItem", 0},
    {"PHandDrill2Item", 0},
    {"PHandDrill3Item", 0},
    {"PHandDrill4Item", 0},
    {"CMedkit", 0},
    {"CPowerkit", 0},
};

public Dictionary<string, string> assemblerNames = new Dictionary<string, string>()
{
    {"CConstruction", "MyObjectBuilder_BlueprintDefinition/ConstructionComponent"},
    {"CDisplay", "MyObjectBuilder_BlueprintDefinition/Display"},
    {"CExplosives", "MyObjectBuilder_BlueprintDefinition/ExplosivesComponent"},
//    {"CExplosives", "ExplosivesComponent"},
    {"CInteriorPlate", "MyObjectBuilder_BlueprintDefinition/InteriorPlate"},
    {"CMedical", "MyObjectBuilder_BlueprintDefinition/MedicalComponent"},
    {"CSteelPlate", "MyObjectBuilder_BlueprintDefinition/SteelPlate"},
    {"CSolarCell", "MyObjectBuilder_BlueprintDefinition/SolarCell"},
    {"CBulletproofGlass", "MyObjectBuilder_BlueprintDefinition/BulletproofGlass"},
    {"CComputer", "MyObjectBuilder_BlueprintDefinition/ComputerComponent"},
    {"CDetector", "MyObjectBuilder_BlueprintDefinition/DetectorComponent"},
    {"CGirder", "MyObjectBuilder_BlueprintDefinition/GirderComponent"},
    {"CLargeTube", "MyObjectBuilder_BlueprintDefinition/LargeTube"},
    {"CSmallTube", "MyObjectBuilder_BlueprintDefinition/SmallTube"},
    {"CMetalGrid", "MyObjectBuilder_BlueprintDefinition/MetalGrid"},
    {"CMotor", "MyObjectBuilder_BlueprintDefinition/MotorComponent"},
    {"CPowerCell", "MyObjectBuilder_BlueprintDefinition/PowerCell"},
    {"CRadioCommunication", "MyObjectBuilder_BlueprintDefinition/RadioCommunicationComponent"},
    {"CReactor", "MyObjectBuilder_BlueprintDefinition/ReactorComponent"},
    {"CSuperconductor", "MyObjectBuilder_BlueprintDefinition/Superconductor"},
    {"CThrust", "MyObjectBuilder_BlueprintDefinition/ThrustComponent"},
    {"AMissile200mm", "Missile200mm"},
    {"ANATO_25x184mm", "NATO_25x184mmMagazine"},
    {"AANATO_5p56x45mm", "ANATO_5p56x45mmMagazine"},
};

public Dictionary<string, string> displayNames = new Dictionary<string, string>()
{
    {"CBulletproofGlass", "CBGlass"},
    {"CRadioCommunication", "CRadioCom"},
};

public List<string> ingotNames = new List<string>()
{
    "ICobalt",
    "IGold",
    "IIron",
    "IMagnesium",
    "INickel",
    "IPlatinum",
    "ISilicon",
    "ISilver",
    "IStone",
    "IUranium",
    "OIce"
};

public List<string> oreNames = new List<string>()
{
    "OCobalt",
    "OGold",
    "OIron",
    "OMagnesium",
    "ONickel",
    "OPlatinum",
    "OSilicon",
    "OSilver",
    "OStone",
    "OUranium",
};

public List<string> cNames = new List<string>()
{
    "CSteelPlate",
    "CInteriorPlate",
    "CConstruction",
    "CBGlass",
    "CGirder",
    "CComputer",
    "CDisplay",
    "CExplosives",
    "CSmallTube",
    "CLargeTube",
    "CMetalGrid",
    "CMotor",
    "CPowerCell",
    "CRadioCom",
    "CReactor",
    "CSolarCell",
    "CThrust",
    "CMedical",
    "CDetector",
    "CGravityGenerator",
    "CSuperconductor",
};