using System;
using Phyros.Athena.Model.ProcessItems;
using Phyros.Athena.Model.Workflow.EventTriggers;

namespace Phyros.Athena.Model.Workflow.EventTriggers
{
	public interface IEventTrigger
	{
		IEventResult Execute(IProcessItem processItem);
	}
}
