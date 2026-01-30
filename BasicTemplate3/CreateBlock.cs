// Copyright (C) ANSYS.  All Rights Reserved.

using BasicTemplate3.Properties;
using SpaceClaim.Api.V252;
using SpaceClaim.Api.V252.Extensibility;
using SpaceClaim.Api.V252.Geometry;
using SpaceClaim.Api.V252.Modeler;
using System.Drawing;

namespace BasicTemplate3
{
    // Every tools and actions should be captured by commands. 
    class CreateBlockCapsule : CommandCapsule
    {
        // The name must match the name specified in the ribbon bar XML.
        public const string CommandName = "BasicTemplate3.CreateBlock";

        public CreateBlockCapsule()
            : base(CommandName, Resources.ButtonText, Resources.CreateBlock, "Creation of an example block")
        {

        }

        protected override void OnUpdate(Command command)
        {
            Window window = Window.ActiveWindow;
            command.IsEnabled = window != null;
        }

        protected override void OnExecute(Command command, ExecutionContext context, Rectangle buttonRect)
        {
            Window window = Window.ActiveWindow;
            window.InteractionMode = InteractionMode.Solid;
            Body body = Body.ExtrudeProfile(new RectangleProfile(Plane.PlaneXY, 0.01, 0.01), 0.02);
            DesignBody desBodyMaster = DesignBody.Create(window.Document.MainPart, "a sample block", body);
        }
    }
}

