using Ansys.Discovery.Api.V252.Application;
using Ansys.Discovery.Api.V252.Physics.Conditions;
using Ansys.Discovery.Api.V252.Physics.Mesh;
using Ansys.Discovery.Api.V252.Physics.Results;
using Ansys.Discovery.Api.V252.Solution;
using Ansys.Discovery.Api.V252.Units;
using BasicTemplate3.Properties;
using SpaceClaim.Api.V252;
using SpaceClaim.Api.V252.Extensibility;
using SpaceClaim.Api.V252.Geometry;
using SpaceClaim.Api.V252.Modeler;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using ScreenPoint = System.Drawing.Point;
using Task = System.Threading.Tasks.Task;

namespace BasicTemplate3
{

    // The element which will control the order of the cutting operations
    static class CuttingOrder
    {
        const string commandName = "BasicTemplate3.CuttingOrder";

        public static void Initialize()
        {
            Command.Create(commandName);
        }

        public static Command Command => Command.GetCommand(commandName);

        public static string Value => Command.Text;
    }

    class SimulationToolCapsule : CommandCapsule
    {
        public const string CommandName = "BasicTemplate3.SimulationTool";

        public SimulationToolCapsule()
            : base(CommandName, "Simulate", Resources.button2, "Creates a custom object")  // TODO
        {
        }

        protected override void OnInitialize(Command command)
        {
            CuttingOrder.Initialize();
            CuttingOrder.Command.IsVisible = true;
        }

        protected override void OnUpdate(Command command)
        {
            Window window = Window.ActiveWindow;
            command.IsEnabled = window != null;
            command.IsChecked = window != null && window.ActiveTool is SimulationTool;
        }

        protected override void OnExecute(Command command, ExecutionContext context, Rectangle buttonRect)
        {
            Window.ActiveWindow.SetTool(new SimulationTool());
        }
    }

    // Selection for bodies
    public class BodySelector : CommandCapsule
    {

        public const string CommandName = "BasicTemplate3.BodySelector";
        public static List<DesignBody> designBodies = new List<DesignBody>();

        public BodySelector()
            : base(CommandName, "Selector", Resources.CreateBlock, "Select all bodies to detect welds between")
        {
        }

        protected override void OnInitialize(Command command)
        {
            // Probably do nothing?
            // Maybe reset the list?
        }

        protected override void OnExecute(Command command, ExecutionContext context, Rectangle buttonRect)
        {
            var selection = Window.ActiveWindow.ActiveContext.Selection;

            if (selection == null)
            {
                Notification.Create(NotificationSeverity.Error, "Nothing selected");
                return;
            }

            foreach (var selected in selection)
            {
                // Find all faces/Edges between bodies
            }
        }

    }

    // The Checkmark used to verify a selection
    public class AcceptCapsule : CommandCapsule
    {
        public static double LASER_RADIUS_m = LengthQuantity.Create(75, LengthUnit.Micrometer).ConvertTo(LengthUnit.Meter).Value;
        public static double LASER_POWER = 4_000d;  // Watt per laser area
        public static TimeQuantity SIM_TIME = TimeQuantity.Create(10, TimeUnit.Microsecond);

        public const string CommandName = "BasicTemplate3.AcceptOrder";
        // public static CuttingObject staticselectedObject = null;

        public AcceptCapsule()
            : base(CommandName, "Simulate", Resources.OK_32px, "Apply the condition")
        {
        }

        protected override void OnInitialize(Command command)
        {
            // 1,8e-8
        }

        protected override void OnExecute(Command command, ExecutionContext context, Rectangle buttonRect)
        {
            var selection = Window.ActiveWindow.ActiveContext.SingleSelection;

            if (selection == null)
            {
                Notification.Create(NotificationSeverity.Error, "Nothing selected");
                return;
            }

            if (!(selection is DesignEdge edge))
                return;

            if (!(edge.Shape.Geometry is Circle circle))
                return; // Must be a circle!

            DesignBody parentBody = edge.Parent;
            Part part = parentBody.Parent;
            var laserStartPoint = edge.Shape.StartPoint;
            var plane = circle.Plane;
            var uvPoint = plane.ProjectPoint(laserStartPoint).Param;
            var circleRadius = circle.Radius;
            var circleCenter = plane.ProjectPoint(circle.Frame.Origin).Param;
            var parentFace = parentBody.Faces.First();
            foreach (var face in parentBody.Faces)
            {
                if (!face.Shape.ContainsPoint(laserStartPoint))
                    continue;

                parentFace = face;
                break;
            }

            // setup visual "laser"
            var profile = new CircleProfile(
                plane,
                LASER_RADIUS_m,
                uvPoint
            );
            var extrusion = Body.ExtrudeProfile(profile, 0.002);    // MAGIC NUMBER

            var laserBody = DesignBody.Create(part, "Laser", extrusion);
            laserBody.SetColor(null, Color.Red);

            // Since we can't move the laser in the sim, we create a small ring that "simulates"
            // what it would look like if the laser would cut out the circle
            var cylinder = DesignBody.Create(
                part,
                "Laser_hack",
                Body.ExtrudeProfile(
                    new CircleProfile(
                        plane,
                        circleRadius + LASER_RADIUS_m
                    ),
                    LengthQuantity.Create(0.05, LengthUnit.Millimeter).ConvertTo(LengthUnit.Meter).Value
                )
            );
            var holeInfo = new HoleCreationInfo();
            holeInfo.HoleDiameter = (circleRadius - LASER_RADIUS_m) * 2;
            Hole.Create(
                cylinder.Faces.First(),
                circle.Frame.Origin,
                holeInfo
            );
            cylinder.SetVisibility(null, false);


            // To "simulate" the piece being cut out, we just remove it prior.
            // I wanted to run the simulation and then remove the body, but the sim does not support that; Thus I remove it in advance
            holeInfo = new HoleCreationInfo();
            holeInfo.HoleDiameter = circleRadius * 2;
            Hole.Create(
                parentFace,
                circle.Frame.Origin,
                holeInfo
            );


            // This is bad, I do not like it.
            // I am hardcoding which face is which. This might change
            // in future versions or even with other profiles!
            var laserFace = cylinder.Faces.First();    // #First is the face that meets the main body

            // Calculate the wattage for the "hack".
            // Since th laser has 4kW of power for an area of 75µm^2*Pi, we calculate the area
            // for the face of the cylinder ("hack") and divide that by the area of the laser.
            // This will give us a ratio of "how many lasers would be in this area" and then multiply that by the 4kW
            var cylinderWattage = (laserFace.Area / (Math.Pow(LASER_RADIUS_m, 2) * Math.PI)) * LASER_POWER;

            // Setup sim
            var sim = Simulation.Create();
            sim.SuppressBody(laserBody, true);    // Otherwise this causes weird effects where the laser body is

            Heat.Create(
                sim,
                laserFace,
                PowerQuantity.Create(cylinderWattage, PowerUnit.Watt)
            );

            // Contact.Create(sim, laserFace, face, ContactType.Bonded);

            // Use a time-dependent sim with ~10µs, since it provides better sim results
            sim.SimulationOptions.CalculationType = CalculationType.Transient;  // Sets it to a time-dependent sim
            sim.SimulationOptions.TimeDependentDuration = SIM_TIME;

            sim.IncludeThermalEffects = true;
            sim.SuppressedBodies.Clear();   // I do not know why, but sometimes the sim removes/supresses bodies
            sim.GlobalFidelity.FidelityApproach = GlobalFidelityApproachSetting.Extreme;    // Makes sure all lasers are considered
            
            // This is required for the sim to appear in the tree on the side
            Simulation.SetCurrentSimulation(sim);
        }

    }

    class SimulationTool : Tool
    {
        static bool isToolInitialized = false;


        public SimulationTool() : base(nameof(SimulationTool), InteractionMode.Solid)
        {
            if (!isToolInitialized)
            {
                Window.WindowSelectionChanged += ActiveWindow_SelectionChanged;
                isToolInitialized = true;
            }
        }

        // Make sure that it points the right xml file
        public override string OptionsXml => Resources.SimToolOptions;

        protected override void OnInitialize()
        {
        }

        protected override void OnEnable(bool enable)
        {
            if (enable)
            {
                CuttingOrder.Command.TextChanged += cuttingOrderCommand_TextChanged;
                Document.DocumentChanged += TreeField_Updated;
                // this.SelectionTypes = new List<Type> { typeof(IDesignBody) };
            }
            else
            {
                CustomObjectMagnitude.Command.TextChanged -= cuttingOrderCommand_TextChanged;
                // this.SelectionTypes = new List<Type> { typeof(IDocObject) };    // Reset to normal/default
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
                // var selectedLoad = CuttingObject.GetWrapper(selection as CustomObject);
                Window.ActiveWindow.SetTool(new SimulationTool());  // REMOVE ME LATER!!!

                /*
                if (selectedLoad != null)
                {
                    Window.ActiveWindow.SetTool(new SimulationTool());
                    AcceptCapsule.staticselectedObject = selectedLoad;
                    CuttingOrder.Command.Text = selectedLoad.RatioQuantity.ToString();
                    CuttingOrder.Command.IsVisible = true;
                    AcceptCapsule.staticselectedObject.AccessRendering = setRendering();
                    AcceptCapsule.staticselectedObject.AccessRendering = setRendering();
                }
                */
            }
        }

        public void cuttingOrderCommand_TextChanged(object sender, CommandTextChangedEventArgs e)
        {
            /*
            if (AcceptCapsule.staticselectedObject == null)
            {
                return;
            }
            */

            int newOrder = int.Parse(e.NewValue);
            WriteBlock.ExecuteTask("Change Magnitude", () => ChangeOrder());

            void ChangeOrder()
            {
                // AcceptCapsule.staticselectedObject.TreeFieldQuantity = RatioQuantity.Create(newOrder.ToString());
            }
        }


        // The event listener to update tree once there is a change in the document. 
        void TreeField_Updated(object sender, DocumentChangedEventArgs e)
        {
            foreach (var changedObj in e.ChangedObjects)
            {
                if (changedObj is CustomObject customobject)
                {
                    // var changedLoad = CuttingObject.GetWrapper(changedObj as CustomObject);

                    /*
                    if ((CuttingOrder.Command.Text != changedLoad.TreeFieldQuantity.ToString()))
                    {
                        CuttingOrder.Command.Text = changedLoad.TreeFieldQuantity.ToString();
                        WriteBlock.ExecuteTask("update order", () => UpdateTreeQuantity(changedLoad));
                    }
                    */
                }
            }
        }

        /*
        private void UpdateTreeQuantity(CuttingObject changedLoad)
        {
            // changedLoad.RatioQuantity = RatioQuantity.Create(changedLoad.TreeFieldQuantity.ToString());
        }
        private Graphic setRendering()
        {
            CuttingObject cut = AcceptCapsule.staticselectedObject;
            CurvePrimitive prim = CurvePrimitive.Create(cut.Edge.Shape);
            Graphic shape = Graphic.Create(null, new List<Primitive> { prim });
            Color color = Color.FromArgb(255, 0, 0);

            Color haloColor = Color.FromArgb(255, color);
            const float haloWidth = 2; // 2 pixels wider on each side

            var style = new GraphicStyle
            {
                ShowWhen = ShowWhen.Preselected,
                LineWidth = cut.Width + 2 * haloWidth,
                LineColor = Color.FromArgb(50, color)

            };
            Graphic preselected = Graphic.Create(style, null, shape);

            Graphic selected;
            {
                float selectedWidth = cut.Width + 2;

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
                LineWidth = cut.Width + 2 * haloWidth,
                LineColor = color
            };

            return Graphic.Create(style, null, new[] { shape, preselected, selected });
        }
        */

    }

    /*
    public class CuttingObject : DiscoveryCustomWrapper<CuttingObject>
    {
        private readonly DesignEdge edge;
        private readonly int width;
        string placement;
        private static double objectCounter;

        // creates a wrapper for an existing custom object
        private CuttingObject(CustomObject subject) : base(subject)
        {
        }

        private CuttingObject(
            DesignEdge desEdge,
            int placement)
            : base(desEdge.Document.MainPart)
        {
            Debug.Assert(desEdge != null);

            this.edge = desEdge;
            this.placement = placement.ToString();
            this.width = 2;

            Group = Groups.BarObject;
            ImageKey = "MaterialsIcon";
        }

        // The create method for the custom bar object
        public static CuttingObject Create(
            DesignEdge desEdge,
            int placement)
        {
            var cObj = new CuttingObject(desEdge, placement);


            objectCounter++;
            cObj.TreeId = "BarObjectsTree";
            cObj.Name = "Cutting Order " + $"{objectCounter}";
            cObj.Initialize();

            desEdge.KeepAlive(true);

            return cObj;
        }

        // I don't know why
        protected override bool IsAlive
        {
            get
            {
                if (edge == null || edge.IsDeleted)
                    return false;
                return true;
            }
        }

        protected override ICollection<IDocObject> Determinants
        {
            get
            {
                return new List<IDocObject> { edge };
            }
        }

        private void UpdateRendering(System.Threading.CancellationToken token)
        {
        }

        public RatioQuantity RatioQuantity
        {
            get { return (RatioQuantity)Quantity.Parse<RatioQuantity>(placement); }
            set
            {
                if (value == (RatioQuantity)Quantity.Parse<RatioQuantity>(placement))
                    return;

                placement = value.ToString();
                WriteBlock.ExecuteTask("Change Magnitude", () => Commit());
            }
        }

        public DesignEdge Edge { get => edge; }

        public float Width { get => width;  }

        public Graphic AccessRendering
        {
            get => Rendering;
            set => Rendering = value;
        }

    }
    */
}
