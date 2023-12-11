using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        public const int STEEL_PLATE_QUOTA = 5000;
        public const int CON_COMP_QUOTA = 3000;
        public const int COMPUTER_QUOTA = 1000;
        public const int INTERIOR_PLATE_QUOTA = 5000;
        public const int SMALL_TUBE_QUOTA = 5000;
        public const int MOTOR_QUOTA = 2000;
        public const int GIRDER_QUOTA = 1000;
        public const int LARGE_TUBE_QUOTA = 1000;
        public const int METAL_GRID_QUOTA = 1000;
        public const int BULLETPROOF_GLASS_QUOTA = 1000;
        public const int DISPLAY_QUOTA = 1000;
        public const int SOLAR_CELL_QUOTA = 1000;
        public const int POWER_CELL_QUOTA = 1000;

        public const String TARGET_ASSEMBLER_NAME = "Auto-Assembler";
        public const String TARGET_OUTPUT_NAME = "Auto-Assembler-Container";
        // This file contains your actual script.
        //
        // You can either keep all your code here, or you can create separate
        // code files to make your program easier to navigate while coding.
        //
        // In order to add a new utility class, right-click on your project, 
        // select 'New' then 'Add Item...'. Now find the 'Space Engineers'
        // category under 'Visual C# Items' on the left hand side, and select
        // 'Utility Class' in the main area. Name it in the box below, and
        // press OK. This utility class will be merged in with your code when
        // deploying your final script.
        //
        // You can also simply create a new utility class manually, you don't
        // have to use the template if you don't want to. Just do so the first
        // time to see what a utility class looks like.
        // 
        // Go to:
        // https://github.com/malware-dev/MDK-SE/wiki/Quick-Introduction-to-Space-Engineers-Ingame-Scripts
        //
        // to learn more about ingame scripts.

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }

        List<IMyCargoContainer> cargos = new List<IMyCargoContainer>();
        List<IMyProductionBlock> assemblers = new List<IMyProductionBlock>();

        public void Save()
        {
        }

        /// <summary>
        /// Inserts a number of components into the target Assembler
        /// </summary>
        /// <param name="component">The desired component type</param>
        /// <param name="number">The number of components</param>
        void QueueComponents(MyItemType component, decimal number)
        {
            Echo("Queuing " + number + " " + component.SubtypeId);
            IMyProductionBlock assembler = GridTerminalSystem.GetBlockWithName(TARGET_ASSEMBLER_NAME) as IMyProductionBlock;

            if (assembler == null)
            {
                Echo("Failed to find assembler");
            }
            else
            {
                MyDefinitionId blueprint = MyDefinitionId.Parse("MyObjectBuilder_BlueprintDefinition/" + ComponentToBlueprint(component));
                if (!assembler.CanUseBlueprint(blueprint))
                {
                    Echo("Can't produce this?!?");
                }
                else
                {
                    assembler.AddQueueItem(blueprint, number);
                }
            }
        }

        /// <summary>
        /// Converts the component Subtype to the blueprint Subtype 
        /// </summary>
        /// <param name="component">The component to convert from</param>
        /// <returns>Blueprint Subtype</returns>
        string ComponentToBlueprint(MyItemType component)
        {
            if (component.SubtypeId == "Computer")
            {
                return "ComputerComponent";
            }
            else if (component.SubtypeId == "Girder")
            {
                return "GirderComponent";
            }
            else if (component.SubtypeId == "Construction")
            {
                return "ConstructionComponent";
            }
            else
            {
                return component.SubtypeId;
            }
        }

        void MoveInventory()
        {
            IMyCargoContainer definedCargo = GridTerminalSystem.GetBlockWithName(TARGET_OUTPUT_NAME) as IMyCargoContainer;
            IMyInventory containerInventory = definedCargo.GetInventory(0);
            IMyProductionBlock assembler = GridTerminalSystem.GetBlockWithName(TARGET_ASSEMBLER_NAME) as IMyProductionBlock;
            IMyInventory assemblerInventory = assembler.GetInventory(1);
            List<MyInventoryItem> assemblersInventoryItems = new List<MyInventoryItem>();
            assemblerInventory.GetItems(assemblersInventoryItems);

            if (containerInventory == null)
            {
                Echo($"Cargo container '{TARGET_OUTPUT_NAME}' not found.");
            }
            foreach (MyInventoryItem items in assemblersInventoryItems)
            {
                assemblerInventory.TransferItemTo(containerInventory, 0, stackIfPossible: true);

            }
        }
        /// <summary>
        /// Search all cargo containers and assemblers for the current count of the given component.
        /// Will also search currently queued components.
        /// </summary>
        /// <param name="component">Component to search for</param>
        /// <returns>Number of components available and queued</returns>
        int GetNumberComponents(MyItemType components)
        {
            IMyProductionBlock assembler = GridTerminalSystem.GetBlockWithName(TARGET_ASSEMBLER_NAME) as IMyProductionBlock;
            IMyCargoContainer definedCargo = GridTerminalSystem.GetBlockWithName(TARGET_OUTPUT_NAME) as IMyCargoContainer;

            int component_count = 0;

            List<MyProductionItem> production_queue = new List<MyProductionItem>();

            component_count += assembler.OutputInventory.GetItemAmount(components).ToIntSafe();
            assembler.GetQueue(production_queue);
            foreach (MyProductionItem queued_item in production_queue)
            {
                if (queued_item.BlueprintId.ToString().Contains(components.SubtypeId))
                {
                    component_count += queued_item.Amount.ToIntSafe();
                }
            }



            component_count += definedCargo.GetInventory().GetItemAmount(components).ToIntSafe();


            return component_count;
        }

        /// <summary>
        /// Check the number of components available verses the provided quota, and then add the difference
        /// to the assembler queue.
        /// </summary>
        /// <param name="component">Target component</param>
        /// <param name="quota">Target quota</param>
        void CheckComponentQuota(String component, int quota)
        {
            MyItemType component_type = new MyItemType("MyObjectBuilder_Component", component);
            int num_components = GetNumberComponents(component_type);
            if (num_components < quota)
            {
                QueueComponents(component_type, quota - num_components);
            }
            else
            {
                Echo("Number of " + component + "s: " + num_components);
            }
        }

        public void Main(string argument, UpdateType updateSource)
        {
            CheckComponentQuota("SteelPlate", STEEL_PLATE_QUOTA);
            CheckComponentQuota("Construction", CON_COMP_QUOTA);
            CheckComponentQuota("Computer", COMPUTER_QUOTA);
            CheckComponentQuota("InteriorPlate", INTERIOR_PLATE_QUOTA);
            CheckComponentQuota("SmallTube", SMALL_TUBE_QUOTA);
            CheckComponentQuota("MotorComponent", MOTOR_QUOTA);
            CheckComponentQuota("Girder", GIRDER_QUOTA);
            CheckComponentQuota("LargeTube", LARGE_TUBE_QUOTA);
            CheckComponentQuota("MetalGrid", METAL_GRID_QUOTA);
            CheckComponentQuota("BulletproofGlass", BULLETPROOF_GLASS_QUOTA);
            CheckComponentQuota("Display", DISPLAY_QUOTA);
            CheckComponentQuota("SolarCell", SOLAR_CELL_QUOTA);
            CheckComponentQuota("PowerCell", POWER_CELL_QUOTA);
            MoveInventory();
        }
    }
}
