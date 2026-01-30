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
using System.Drawing;
using System.Linq;

using Point = SpaceClaim.Api.V252.Geometry.Point;
using Plane = SpaceClaim.Api.V252.Geometry.Plane;

namespace BasicTemplate3
{

    public class AcceptCapsule : CommandCapsule
    {
        public static double LASER_RADIUS_m = LengthQuantity.Create(75, LengthUnit.Micrometer).ConvertTo(LengthUnit.Meter).Value;
        public static double LASER_POWER = 4_000d;  // Watt per laser area
        public static TimeQuantity SIM_TIME = TimeQuantity.Create(10, TimeUnit.Microsecond);

        public const string CommandName = "BasicTemplate3.AcceptOrder";

        public AcceptCapsule()
            : base(CommandName, "Simulate", Resources.OK_32px, "Apply the condition")
        { }

        protected override void OnInitialize(Command command)
        { }

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
            var parentFace = GetFaceWithPoint(parentBody, edge.Shape.StartPoint);   // We just need any point on the circle, doesn't have to be the start point
            var plane = circle.Plane;
            var circleRadius = circle.Radius;

            // Small red rod that is supposed to represent a "Laser"
            var laserBody = CreateLaserBody(part, plane, plane.ProjectPoint(edge.Shape.StartPoint).Param);

            // Since we can't move the laser in the sim, we create a small ring that "simulates"
            // what it would look like if the laser would cut out the circle
            var laserHack = CreateLaserSimHack(part, plane, circle.Frame.Origin, circleRadius);


            // To "simulate" the piece being cut out, we just remove it prior.
            // I wanted to run the simulation and then remove the body, but the sim does not support that; Thus I remove it in advance
            var holeInfo = new HoleCreationInfo();
            holeInfo.HoleDiameter = circleRadius * 2;
            Hole.Create(
                parentFace,
                circle.Frame.Origin,
                holeInfo
            );


            // This is bad, I do not like it.
            // I am hardcoding which face is which. This might change
            // in future versions or even with other profiles!
            var laserFace = laserHack.Faces.First();    // #First is the face that meets the main body

            var sim = SetupSimulation(laserBody, laserFace);

            // This is required for the sim to appear in the tree on the side
            Simulation.SetCurrentSimulation(sim);
        }


        static DesignFace GetFaceWithPoint(DesignBody body, Point point)
        {
            foreach (var face in body.Faces)
            {
                if (!face.Shape.ContainsPoint(point))
                    continue;

                return face;
            }

            throw new Exception("Point is not on any face of the body");
        }

        static DesignBody CreateLaserBody(Part part, Plane plane, PointUV circleOrigin)
        {
            var profile = new CircleProfile(
            plane,
                LASER_RADIUS_m,
                circleOrigin
            );
            var extrusion = Body.ExtrudeProfile(profile, 0.002);    // MAGIC NUMBER

            var laserBody = DesignBody.Create(part, "Laser", extrusion);
            laserBody.SetColor(null, Color.Red);
            return laserBody;
        }

        static DesignBody CreateLaserSimHack(Part part, Plane plane, Point circleOrigin, double circleRadius)
        {
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
                circleOrigin,
                holeInfo
            );
            cylinder.SetVisibility(null, false);

            return cylinder;
        }

        static PowerQuantity CalcLaserPower(IDesignFace laserFace)
        {
            // Calculate the wattage for the "hack".
            // Since th laser has 4kW of power for an area of 75µm^2*Pi, we calculate the area
            // for the face of the cylinder ("hack") and divide that by the area of the laser.
            // This will give us a ratio of "how many lasers would be in this area" and then multiply that by the 4kW
            return PowerQuantity.Create(
                (laserFace.Area / (Math.Pow(LASER_RADIUS_m, 2) * Math.PI)) * LASER_POWER,
                PowerUnit.Watt
            );
        }
    
        static Simulation SetupSimulation(DesignBody laserBody, DesignFace laserFace)
        {
            var sim = Simulation.Create();
            sim.SuppressBody(laserBody, true);    // Otherwise this causes weird effects where the laser body is

            // Creates a simulation condition which heats the face by a certain amount of wattage
            // View Ansys.Discovery.Api.V252.Physics.Conditions for other sim conditions
            Heat.Create(
                sim,
                laserFace,
                CalcLaserPower(laserFace)
            );

            // Use a time-dependent sim with ~10µs, since it provides better sim results
            sim.SimulationOptions.CalculationType = CalculationType.Transient;  // Sets it to a time-dependent sim
            sim.SimulationOptions.TimeDependentDuration = SIM_TIME;

            sim.IncludeThermalEffects = true;
            sim.SuppressedBodies.Clear();   // I do not know why, but sometimes the sim removes/supresses bodies
            sim.GlobalFidelity.FidelityApproach = GlobalFidelityApproachSetting.Extreme;    // Makes sure all lasers are considered

            return sim;
        }

    }

}
