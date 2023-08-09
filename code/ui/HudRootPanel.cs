using Sandbox.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClockBlockers.UI;

public class HUDRootPanel : RootPanel
{
	public static HUDRootPanel? Instance { get; private set; }

	public HUDRootPanel()
	{
		if (Instance != null)
		{
			Instance.Delete();
		}
		Instance = this;

		AddChild<Crosshair>();
	}
}
