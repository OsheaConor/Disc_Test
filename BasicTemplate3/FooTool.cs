// Copyright (C) ANSYS.  All Rights Reserved.


using Ansys.Discovery.Api.V252.Physics.Conditions;
using Ansys.Discovery.Api.V252.Physics.Materials;
using Ansys.Discovery.Api.V252.Physics.Results;
using Ansys.Discovery.Api.V252.Solution;
using Ansys.Discovery.Api.V252.Units;
using BasicTemplate3.Properties;
using SpaceClaim.Api.V252;
using SpaceClaim.Api.V252.Extensibility;
using SpaceClaim.Api.V252.Geometry;
using SpaceClaim.Api.V252.Unsupported.RuledCutting;
using System;
using System.Drawing;
using System.Linq;

namespace BasicTemplate3
{
    static class CheckBox
    {
        const string commandName = "BasicTemplate3.CheckBox";

        public static void Initialize()
        {
            Command command = Command.Create(commandName);
        }

        public static Command Command
        {
            get { return Command.GetCommand(commandName); }
        }

        public static string Value
        {
            get
            {
                var state = Command.Text;
                return state;
            }
            set
            {

                Command.Text = value;
            }
        }
    }

    static class TextBoxDouble
    {
        const string commandName = "BasicTemplate3.TextBoxDouble";

        public static void Initialize()
        {
            Command command = Command.Create(commandName);
        }

        public static Command Command
        {
            get { return Command.GetCommand(commandName); }
        }

        public static TemperatureQuantity Value => (TemperatureQuantity)Quantity.Parse<TemperatureQuantity>(Command.Text);
    }

    static class TextBoxQuantity
    {
        const string commandName = "BasicTemplate3.TextBoxQuantity";

        public static void Initialize()
        {
            Command command = Command.Create(commandName);
        }

        public static Command Command
        {
            get { return Command.GetCommand(commandName); }
        }

        public static TemperatureQuantity Value => (TemperatureQuantity)Quantity.Parse<TemperatureQuantity>(Command.Text);
    }

    class FooToolCapsule : CommandCapsule
    {
        public const string CommandName = "BasicTemplate3.FooTool";

        public FooToolCapsule()
            : base(CommandName, "Tool example", Resources.button1, "Invokes a tool")
        {
        }

        protected override void OnInitialize(Command command)
        {
            TextBoxDouble.Initialize();
            TextBoxDouble.Command.IsVisible = false;
            TextBoxQuantity.Initialize();
            TextBoxQuantity.Command.IsVisible = true;
            CheckBox.Initialize();
            CheckBox.Command.IsVisible = true;
        }

        protected override void OnUpdate(Command command)
        {
            Window window = Window.ActiveWindow;
            command.IsEnabled = window != null;
            command.IsChecked = window != null && window.ActiveTool is FooTool;
        }

        protected override void OnExecute(Command command, ExecutionContext context, Rectangle buttonRect)
        {
            var laserBody = Window.ActiveWindow.Document.MainPart.Bodies.Where(b => b.Name.Equals("Laser")).First();

                WriteBlock.AppendTask(() =>
                {
                    const int numFrames = 360;
                    const double stepSize = (2 * Math.PI) / (double)numFrames;

                    for (int i = 0; i < numFrames; i++)
                    {
                        laserBody.Transform(
                               Matrix.CreateTranslation(Vector.Create(0, LengthQuantity.Create(4, LengthUnit.Millimeter).ConvertTo(LengthUnit.Meter).Value, 0))
                               * Matrix.CreateRotation(Line.Create(SpaceClaim.Api.V252.Geometry.Point.Create(0, 0, 0), Direction.DirY), stepSize * i)
                               * Matrix.CreateTranslation(Vector.Create(0, -LengthQuantity.Create(4, LengthUnit.Millimeter).ConvertTo(LengthUnit.Meter).Value, 0))
                           );
                        System.Threading.Thread.Sleep(200);
                    }
                });

        }
    }

    /// <summary>
    /// The class that implements the tool generated on the hud when it is invoked by the button. 
    /// </summary>
    class FooTool : Tool
    {
        static bool isToolInitialized = false;
        string isChecked;
        int textBoxDoubleValue;


        public FooTool() : base(nameof(FooTool), InteractionMode.Solid)
        {
            if (!isToolInitialized)
            {
                //Window.WindowSelectionChanged += ActiveWindow_SelectionChanged;
                isToolInitialized = true;
            }
        }

        // Make sure that it points the right xml file
        public override string OptionsXml => Resources.FooToolOptions;

        protected override void OnInitialize()
        {
            isChecked = CheckBox.Value;

        }

        protected override void OnEnable(bool enable)
        {
            if (enable)
            {
                CheckBox.Command.TextChanged += checkboxCommand_TextChanged;
                TextBoxDouble.Command.TextChanged += textboxdoubleCommand_TextChanged;
                TextBoxQuantity.Command.TextChanged += textboxquantityCommand_TextChanged;
            }
            else
            {
                CheckBox.Command.TextChanged -= checkboxCommand_TextChanged;
                TextBoxDouble.Command.TextChanged -= textboxdoubleCommand_TextChanged;
                TextBoxQuantity.Command.TextChanged -= textboxquantityCommand_TextChanged;
            }
        }

        void checkboxCommand_TextChanged(object sender, CommandTextChangedEventArgs e)
        {
            isChecked = CheckBox.Value;

            if (CheckBox.Command.IsChecked)
            {
                TextBoxDouble.Command.IsVisible = true;
                TextBoxQuantity.Command.IsVisible = false;
            }
            else
            {
                TextBoxDouble.Command.IsVisible = false;
                TextBoxQuantity.Command.IsVisible = true;
            }

        }

        void textboxdoubleCommand_TextChanged(object sender, CommandTextChangedEventArgs e)
        {
            var tempQuantity = TemperatureQuantity.Create(Double.Parse(e.NewValue), TemperatureUnit.Kelvin);
            TextBoxQuantity.Command.Text = tempQuantity.ToString();
        }

        void textboxquantityCommand_TextChanged(object sender, CommandTextChangedEventArgs e)
        {
            var tempMagnitude = (TemperatureQuantity)Quantity.Parse<TemperatureQuantity>(e.NewValue);
            TextBoxDouble.Command.Text = tempMagnitude.Value.ToString();
        }


    }


}
