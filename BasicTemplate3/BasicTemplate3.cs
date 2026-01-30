// Copyright (C) ANSYS.  All Rights Reserved.
using BasicTemplate3.Properties;
using SpaceClaim.Api.V252.Extensibility;
using System.ComponentModel.Composition;
using System.Collections.Generic;
using System;
using ITreeHierarchy = Ansys.Discovery.Api.V252.Customization.Tree.ITreeHierarchy;

namespace BasicTemplate3
{
    public class BasicTemplate3 : AddIn, IExtensibility, ICommandExtensibility, IRibbonExtensibility
    {
        readonly CommandCapsule[] capsules = new CommandCapsule[] {
            new CreateBlockCapsule(),
            new FooToolCapsule(),
            new BarToolCapsule(),
            new TickMarkCapsule(),

            new SimulationToolCapsule(),
            new AcceptCapsule(),
            new BodySelector(),
        };

        public bool Connect()
        {
            return true;
        }

        public void Disconnect()
        {

        }

        public void Initialize()
        {
            foreach (CommandCapsule capsule in capsules)
            {
                capsule.Initialize();
            }


            SpaceClaim.Api.V252.Application.AddSelectionHandler(new CustomBarObjectSelectionHandler());

        }

        public string GetCustomUI()
        {
            /*
			 * This method is called during startup.  The 'command' attributes in the XML refer
			 * to the names of Command objects created during the Initialize method, which will
			 * have already been called at this point.
			 */
            return Resources.Ribbon;
        }
    }

    /* The class that addin needs to define and export for Discovery to render and manage the addin tree.
     * Addin needs to implement ITreeHierarchy
     * */
    [Export(typeof(ITreeHierarchy))]
    public class BarTreeHierarchy : ITreeHierarchy
    {
        public string Id => "BarObjectsTree";

        public int Index => 0;

        public string TreeName => "BarObjectsTree";

        public string RootNodeImageKey => "Test Addin";

        public string DefaultGroup => Groups.None;

        public List<string> RootGroups => new List<string>() { Groups.BarObject };

        public Dictionary<string, List<string>> SubGroupMapping => new Dictionary<string, List<string>>()
        {
             { Groups.BarObject, new List<string>() { Groups.CustomMagnitude} },
        };

        public IDictionary<string, string> GroupsToDisplayNames => new Dictionary<string, string>()
        {
            { Groups.CustomMagnitude, "Load Magnitude" }
        };

        public IDictionary<string, System.Drawing.Image> GroupsToImages => new Dictionary<string, System.Drawing.Image>()
        {
            { Groups.CustomMagnitude, Resources.button1}
        };

        public IDictionary<string, string> GroupsToImageKeys => new Dictionary<string, string>();

        public IEnumerable<string> StageIdentifiers => new string[] { "ModelStage", "ExploreStage", "RefineStage"};

        public int DefaultGroupingThreshold => 1;
        public bool UseParentGroupingThresholds => true;
        public bool CountIndirectChildren => true;
        public bool IgnoreThresholdAfterFirstSubGroup => true;
        public bool UseDeepGrouping => false;

        public bool IsDragEnabled => false;

        public bool SortUsingSubGroupMapping => true;

        public Func<IReadOnlyDictionary<string, object>, IReadOnlyDictionary<string, object>, bool> CanDrop => CanDropOnTarget;

        public Action<IReadOnlyDictionary<string, object>, IReadOnlyDictionary<string, object>> DropCompleted => throw new NotImplementedException();

        public Func<IReadOnlyDictionary<string, object>, string> GetTreeGroup => throw new NotImplementedException();

        public Func<IReadOnlyDictionary<string, object>, string> GetTreeToolTip => throw new NotImplementedException();

        private bool CanDropOnTarget(IReadOnlyDictionary<string, object> sourceInfo, IReadOnlyDictionary<string, object> targetInfo)
        {

            return false;
        }

        
    }

    /// <summary>
    /// A class to define the hierachy within tree objects.
    /// </summary>
    public class Groups
    {
        public const string None = "None";
        public const string BarObject = "BarObjects";
        public const string CustomMagnitude = "CustomMagnitude";
    }
}
