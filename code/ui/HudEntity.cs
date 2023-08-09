using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClockBlockers.UI;

public class ClockBlockersHud : HudEntity<HUDRootPanel>
{
	public static ClockBlockersHud? Instance { get; private set;}

	public ClockBlockersHud()
	{
		Instance = this;
	}
}
