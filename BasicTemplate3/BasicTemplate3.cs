// Copyright (C) ANSYS.  All Rights Reserved.
using BasicTemplate3.Properties;
using SpaceClaim.Api.V252.Extensibility;

namespace BasicTemplate3
{
    public class BasicTemplate3 : AddIn, IExtensibility, ICommandExtensibility, IRibbonExtensibility
    {
        readonly CommandCapsule[] capsules = new CommandCapsule[] {
            new CreateBlockCapsule(),
            new AcceptCapsule(),
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
        }

        public string GetCustomUI()
        {
            return Resources.Ribbon;
        }
    }

}
