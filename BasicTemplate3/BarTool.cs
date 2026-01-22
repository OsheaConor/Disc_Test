// Copyright (C) ANSYS.  All Rights Reserved.

using Ansys.Discovery.Api.V241.Customization.Wrapping;
using Ansys.Discovery.Api.V241.Physics.Conditions;
using Ansys.Discovery.Api.V241.Solution;
using Ansys.Discovery.Api.V241.Units;
using BasicTemplate3.Properties;
using SpaceClaim.Api.V241;
using SpaceClaim.Api.V241.Display;
using SpaceClaim.Api.V241.Extensibility;
using SpaceClaim.Api.V241.Geometry;
using SpaceClaim.Api.V241.Modeler;
using SpaceClaim.Api.V241.Unsupported.RuledCutting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using ScreenPoint = System.Drawing.Point;

namespace BasicTemplate3
{
    static class CustomObjectMagnitude
    {
        const string commandName = "BasicTemplate3.CustomObjectMagnitude";

        public static void Initialize()
        {
            Command.Create(commandName);
        }

        public static Command Command => Command.GetCommand(commandName);

        public static RatioQuantity Value => (RatioQuantity)Quantity.Parse<RatioQuantity>(Command.Text);
    }

    class BarToolCapsule : CommandCapsule
    {
        public const string CommandName = "BasicTemplate3.BarTool";

        public BarToolCapsule()
            : base(CommandName, "Custom object", Resources.button2, "Creates a custom object")
        {
        }

        protected override void OnInitialize(Command command)
        {
            CustomObjectMagnitude.Initialize();
            CustomObjectMagnitude.Command.IsVisible = true;
        }

        protected override void OnUpdate(Command command)
        {
            Window window = Window.ActiveWindow;
            command.IsEnabled = window != null;
            command.IsChecked = window != null && window.ActiveTool is BarTool;
        }

        protected override void OnExecute(Command command, ExecutionContext context, Rectangle buttonRect)
        {
            Window.ActiveWindow.SetTool(new BarTool());
        }
    }

    public class TickMarkCapsule: CommandCapsule
    {
        public const string CommandName = "BasicTemplate3.TickMark";
        public static BarObject staticselectedObject = null;

        public TickMarkCapsule()
            : base(CommandName, "Custom object", Resources.OK_32px, "Apply the condition")
        {
        }

        protected override void OnInitialize(Command command)
        {

        }

        protected override void OnExecute(Command command, ExecutionContext context, Rectangle buttonRect)
        {
            var selection = Window.ActiveWindow.ActiveContext.Selection;

            List<IDesignFace> designFaces = new List<IDesignFace>();

            foreach (var item in selection)
            {
                designFaces.Add(item as IDesignFace);
            }

            if (designFaces.Count == 0)
            {
                return;
            }

            if (designFaces == null)
            {
                staticselectedObject = null;
                
            }
            if (staticselectedObject == null)
            {
                WriteBlock.ExecuteTask("Create Force", () => {
                var obj = BarObject.Create(designFaces, CustomObjectMagnitude.Value.ToString());
                });
            }
        }

    }

    /// <summary>
    /// The class that implements the tool generated on the hud when it is invoked by the button. 
    /// </summary>
    class BarTool : Tool
    {
        static bool isToolInitialized = false;
        

        public BarTool() : base(nameof(BarTool), InteractionMode.Solid)
        {
            if (!isToolInitialized)
            {
                Window.WindowSelectionChanged += ActiveWindow_SelectionChanged;
                isToolInitialized = true;
            }
        }

        // Make sure that it points the right xml file
        public override string OptionsXml => Resources.BarToolOptions;

        protected override void OnInitialize()
        {
        }

        protected override void OnEnable(bool enable)
        {
            if (enable)
            {
                CustomObjectMagnitude.Command.TextChanged += barObjectMagnitudeCommand_TextChanged;
                Document.DocumentChanged += TreeField_Updated;
            }
            else
            {
                CustomObjectMagnitude.Command.TextChanged -= barObjectMagnitudeCommand_TextChanged;
            }
        }

        protected override bool OnClickStart(ScreenPoint cursorPos, Line cursorRay)
        {
            return false;
        }

        protected override bool OnClickEvent(ScreenPoint cursorPos, Line cursorRay)
        {
            return false;
        }

        public void ActiveWindow_SelectionChanged(object sender, EventArgs e)
        {
            if (Window.ActiveWindow == null)
            {
                return;
            }
            var selection = Window.ActiveWindow.ActiveContext.SingleSelection;
            if (selection is ICustomObject)
            {
                var selectedLoad = BarObject.GetWrapper(selection as CustomObject);

                if (selectedLoad != null)
                {
                    Window.ActiveWindow.SetTool(new BarTool());
                    TickMarkCapsule.staticselectedObject = selectedLoad;
                    CustomObjectMagnitude.Command.Text = selectedLoad.RatioQuantity.ToString();
                    CustomObjectMagnitude.Command.IsVisible = true;
                    TickMarkCapsule.staticselectedObject.AccessRendering = setRendering();
                    TickMarkCapsule.staticselectedObject.AccessRendering = setRendering();
                }
            }
        }

        private Graphic setRendering()
            {
            List<DesignFace> faces = TickMarkCapsule.staticselectedObject.AllFaces.ToList();
            Graphic shape = Graphic.Create(null, BarObject.GetPrimitives(faces));
            Color color = Color.FromArgb(255, 0, 0);

            Color haloColor = Color.FromArgb(255, color);
            const float haloWidth = 2; // 2 pixels wider on each side

            var style = new GraphicStyle
            {
                ShowWhen = ShowWhen.Preselected,
                LineWidth = TickMarkCapsule.staticselectedObject.Width + 2 * haloWidth,
                LineColor = Color.FromArgb(50, color)

            };
            Graphic preselected = Graphic.Create(style, null, shape);

            Graphic selected;
            {
                float selectedWidth = TickMarkCapsule.staticselectedObject.Width + 2;

                style = new GraphicStyle
                {
                    ShowWhen = ShowWhen.Preselected,
                    LineWidth = selectedWidth + 2 * haloWidth,
                    LineColor = haloColor
                };
                Graphic selectedAndPreselected = Graphic.Create(style, null, shape);

                style = new GraphicStyle
                {
                    ShowWhen = ShowWhen.Selected,
                    LineWidth = selectedWidth
                };
                selected = Graphic.Create(style, null, shape, selectedAndPreselected);
            }

            style = new GraphicStyle
            {
                ShowWhen = ShowWhen.Selected,
                LineWidth = TickMarkCapsule.staticselectedObject.Width,
                LineColor = color
            };

            return Graphic.Create(style, null, new[] { shape, preselected, selected });
        }

        public void barObjectMagnitudeCommand_TextChanged(object sender, CommandTextChangedEventArgs e)
        {
            if (TickMarkCapsule.staticselectedObject == null)
            {
                return;
            }

            RatioQuantity newQuantity = (RatioQuantity)Quantity.Parse<RatioQuantity>(e.NewValue);
            WriteBlock.ExecuteTask("Change Magnitude", () => ChangeMagnitude());

            void ChangeMagnitude()
            {
                TickMarkCapsule.staticselectedObject.RatioQuantity = newQuantity;
                TickMarkCapsule.staticselectedObject.TreeFieldQuantity = newQuantity;
            }
        }


        // The event listener to update tree once there is a change in the document. 
        void TreeField_Updated(object sender, DocumentChangedEventArgs e)
        {
            foreach (var changedObj in e.ChangedObjects)
            {
                if (changedObj is CustomObject customobject)
                {
                    var changedLoad = BarObject.GetWrapper(changedObj as CustomObject);

                    if ((CustomObjectMagnitude.Command.Text != changedLoad.TreeFieldQuantity.ToString()))
                    {
                        CustomObjectMagnitude.Command.Text = changedLoad.TreeFieldQuantity.ToString();
                        WriteBlock.ExecuteTask("update quantity", () => UpdateTreeQuantity(changedLoad));
                    }               
                }
            }
        }

        private void UpdateTreeQuantity(BarObject changedLoad)
        {
            changedLoad.RatioQuantity = (RatioQuantity)Quantity.Parse<RatioQuantity>(changedLoad.TreeFieldQuantity.ToString());
        }

    }

    /* 
     * A Custom object definition to be used in the addin.
     * Bar object will use some simulation and tree features therefore it is using a PHysicsCustomWrapper.
     */
    public class BarObject : PhysicsCustomWrapper<BarObject>
    {
        private readonly DesignFace designFace;
        private readonly List<DesignFace> desFaces = new List<DesignFace>();
        private readonly int width;
        string quantity;
        private static double barObjectCounter;

        // creates a wrapper for an existing custom object
        public BarObject(CustomObject subject) : base(subject)
        {
        }

        public BarObject(
            List<IDesignFace> desFaces,
            string quantity)
            : base(desFaces.First().Document.MainPart)
        {
            Debug.Assert(desFaces.First() != null);

            foreach (var face in desFaces)
            {
                var designFace = face as DesignFace ?? face.Master;
                this.desFaces.Add(designFace);
            }
            this.designFace = desFaces.First() as DesignFace ;
            this.quantity = quantity;
            this.width = 2;

            Group = Groups.BarObject;
            ImageKey = "MaterialsIcon";
        }

        // The create method for the custom bar object
        public static BarObject Create(
            List<IDesignFace> desFaces,
            string quantity)
        {
            var customBarObject = new BarObject(desFaces, quantity);


            barObjectCounter++;
            customBarObject.TreeId = "BarObjectsTree";
            customBarObject.TreeFieldQuantity = customBarObject.RatioQuantity;  // Tree fields needs a dimensional quantity therefore ratio unit is used here.
            customBarObject.Name = "Bar Object " + $"{barObjectCounter}";
            // Reverse order makes Discovery Crash.
            customBarObject.Apply();
            customBarObject.Initialize();
            
            foreach (var item in desFaces)
            {
                item.KeepAlive(true);
            }

            return customBarObject;
        }

        // I don't know why
        protected override bool IsAlive
        {
            get
            {
                foreach (var face in desFaces)
                {
                    if (face == null || face.IsDeleted)
                        return false;
                }
                return true;
            }
        }

        protected override ICollection<IDocObject> Determinants
        {
            get
            {
                return desFaces.ToArray();
            }
        }

        public static IDocObject DesignFaceToIDocObject(DesignFace df)
        {
            return df;
        }

        protected override bool Update()
        {
#if false
			DisplayImage = new DisplayImage(0, 1);
			UpdateRendering(CancellationToken.None);
			return true;
			// update was done
#else
            return false; // update was not done - use async update
#endif
        }

        protected override void UpdateAsync(System.Threading.CancellationToken token)
        {
            DisplayImage = new DisplayImage(0, 1);
            UpdateRendering(token);
        }

        private void UpdateRendering(System.Threading.CancellationToken token)
        {
        }

 
        public static ICollection<Primitive> GetPrimitives(List<DesignFace> faces)
        {
            return GetPrimitives(faces, System.Threading.CancellationToken.None);
        }

        private static ICollection<Primitive> GetPrimitives(List<DesignFace> faces, System.Threading.CancellationToken token)
        {
            Debug.Assert(faces != null);
            var primitives = new List<Primitive>();

            foreach (DesignFace desFace in faces)
            {
                Debug.Assert(desFace != null);
                Face face = desFace.Shape;

                foreach (Loop loop in face.Loops)
                {
                    ICollection<Fin> fins = loop.Fins;
                    Edge[] trimmedCurves = fins.Select(fin => fin.Edge).ToArray();

                    Fin firstFin = fins.First();
                    Edge firstEdge = trimmedCurves[0];

                    var profile = new List<ITrimmedCurve>();
                    foreach (var fin in loop.Fins)
                    {
                        profile.Add(fin.Edge);
                    }

                    primitives.AddRange(profile.Select(CurvePrimitive.Create));
                }
            }
   

            return primitives;
        }

        /// <summary>
        /// Getter for the design face property of the object.
        /// </summary>
        public DesignFace Face
        {
            get { return designFace; }
        }

        public List<DesignFace> AllFaces
        {
            get { return desFaces; }
        }

        public float Width
        {
            get { return width; }
        }


        public RatioQuantity RatioQuantity
        {
            get { return (RatioQuantity)Quantity.Parse<RatioQuantity>(quantity); }
            set
            {
                if (value == (RatioQuantity)Quantity.Parse<RatioQuantity>(quantity))
                    return;

                quantity = value.ToString();
                WriteBlock.ExecuteTask("Change Magnitude", () => Commit());
            }
        }


        public Graphic AccessRendering
        {
            get => Rendering;
            set => Rendering = value;
        }

    }


    public class CustomBarObjectSelectionHandler : SelectionHandler<CustomObject>
    {
        public override Tool GetToolOnFullEdit(CustomObject selectedObject)
        {
            var customLoad = CustomWrapper<BarObject>.GetWrapper(selectedObject);
            if (customLoad != null)
            {
                WriteBlock.ExecuteTask(
                    "Set Force Magnitude",
                    () =>
                    {
                        var magnitude = Command.GetCommand("BasicTemplate3.CustomObjectMagnitude");
                        magnitude.Text = customLoad.RatioQuantity.ToString();
                    }
                );
                return new BarTool();
            }
            else
            {
                return base.GetToolOnFullEdit(selectedObject);
            }
        }
    }
}
