/*=============================================================================|
|  PROJECT RSMoveMecha                                                   1.0.0 |
|==============================================================================|
|  Copyright (C) 2018 Denis FRAIPONT (SICA2M)                                  |
|  All rights reserved.                                                        |
|==============================================================================|
|  RSMoveMecha is free software: you can redistribute it and/or modify         |
|  it under the terms of the Lesser GNU General Public License as published by |
|  the Free Software Foundation, either version 3 of the License, or           |
|  (at your option) any later version.                                         |
|                                                                              |
|  It means that you can distribute your commercial software which includes    |
|  RSMoveMecha without the requirement to distribute the source code           |
|  of your application and without the requirement that your application be    |
|  itself distributed under LGPL.                                              |
|                                                                              |
|  RSMoveMecha    is distributed in the hope that it will be useful,           |
|  but WITHOUT ANY WARRANTY; without even the implied warranty of              |
|  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the               |
|  Lesser GNU General Public License for more details.                         |
|                                                                              |
|  You should have received a copy of the GNU General Public License and a     |
|  copy of Lesser GNU General Public License along with RSMoveMecha.           |
|  If not, see  http://www.gnu.org/licenses/                                   |
|=============================================================================*/

using System;
using System.Collections.Generic;

using ABB.Robotics.Controllers;
using ABB.Robotics.Controllers.Discovery;
using ABB.Robotics.Controllers.RapidDomain;
using ABB.Robotics.RobotStudio;
using ABB.Robotics.RobotStudio.Controllers;
using ABB.Robotics.RobotStudio.Stations;

namespace RSMoveMecha
{
	/// <summary>
	/// Code-behind class for the RSMoveMecha Smart Component.
	/// </summary>
	/// <remarks>
	/// The code-behind class should be seen as a service provider used by the 
	/// Smart Component runtime. Only one instance of the code-behind class
	/// is created, regardless of how many instances there are of the associated
	/// Smart Component.
	/// Therefore, the code-behind class should not store any state information.
	/// Instead, use the SmartComponent.StateCache collection.
	/// </remarks>
	public class CodeBehind : SmartComponentCodeBehind
	{
		//Principal Component is OnLoad (don't do anything)
		bool bOnLoad = false;
		//Private Component List for EventHandler
		private List<SmartComponent> myComponents = new List<SmartComponent>();
		//Last time an update occurs (when received a lot of event log in same time).
		DateTime lastUpdate = DateTime.UtcNow;
		//If component is on OnPropertyValueChanged
		Dictionary<string, bool> isOnPropertyValueChanged;
		//If component is on UpdateControllers
		Dictionary<string, bool> isUpdatingControllers;

		/// <summary>
		/// Called from [!:SmartComponent.InitializeCodeBehind]. 
		/// </summary>
		/// <param name="component">Smart Component</param>
		public override void OnInitialize(SmartComponent component)
		{
			///Never Called???
			base.OnInitialize(component);
			component.Properties["Status"].Value = "OnInitialize";
		}

		/// <summary>
		/// Called if the library containing the SmartComponent has been replaced
		/// </summary>
		/// <param name="component">Smart Component</param>
		public override void OnLibraryReplaced(SmartComponent component)
		{
			base.OnLibraryReplaced(component);
			component.Properties["Status"].Value = "OnLibraryReplaced";

			//Save Modified Component for EventHandlers
			//OnPropertyValueChanged is not called here
			UpdateComponentList(component);
		}

		/// <summary>
		/// Called when the library or station containing the SmartComponent has been loaded 
		/// </summary>
		/// <param name="component">Smart Component</param>
		public override void OnLoad(SmartComponent component)
		{
			base.OnLoad(component);
			bOnLoad = true;
			isOnPropertyValueChanged = new Dictionary<string, bool>();
			isUpdatingControllers = new Dictionary<string, bool>();
			component.Properties["Status"].Value = "OnLoad";
			//Here component is not the final component and don't get saved properties.
			//Only called once for all same instance.
			//Connect Controller eventHandler
			//ABB.Robotics.RobotStudio.Controllers.ControllerReferenceChangedEventHandler
			ControllerManager.ControllerAdded -= OnControllerAdded;
			ControllerManager.ControllerAdded += OnControllerAdded;
			ControllerManager.ControllerRemoved -= OnControllerRemoved;
			ControllerManager.ControllerRemoved += OnControllerRemoved;
			//ActiveStation is null here at startup
			component.ContainingProject.Saving -= OnSaving;
			component.ContainingProject.Saving += OnSaving;

			// Survey log message for checked programs at end of apply.
			Logger.LogMessageAdded -= OnLogMessageAdded;
			Logger.LogMessageAdded += OnLogMessageAdded;

			bOnLoad = false;
		}

		/// <summary>
		/// Called when the value of a dynamic property value has changed.
		/// </summary>
		/// <param name="component"> Component that owns the changed property. </param>
		/// <param name="changedProperty"> Changed property. </param>
		/// <param name="oldValue"> Previous value of the changed property. </param>
		public override void OnPropertyValueChanged(SmartComponent component, DynamicProperty changedProperty, Object oldValue)
		{
			if (changedProperty.Name == "Status")
				return;

			base.OnPropertyValueChanged(component, changedProperty, oldValue);
			if (bOnLoad)
			{
				UpdateComponentList(component);
				UpdateControllers(component);
				isOnPropertyValueChanged[component.UniqueId] = false;
				return;
			}
			if (!isOnPropertyValueChanged.ContainsKey(component.UniqueId))
				isOnPropertyValueChanged[component.UniqueId] = false;

			bool bIsOnPropertyValueChanged = isOnPropertyValueChanged[component.UniqueId];
			isOnPropertyValueChanged[component.UniqueId] = true;

			if (changedProperty.Name == "Controller")
			{
				if ((string)changedProperty.Value != "Update")
				{
					if (GetController(component) != null)
					{
						Logger.AddMessage("RSMoveMecha: Connecting Component " + component.Name + " to " + (string)changedProperty.Value, LogMessageSeverity.Information);
						component.Properties["Status"].Value = "Connected";
					}
				}
			}
			if (changedProperty.Name == "CtrlMechanism")
			{
				GetController(component);
			}
			if (changedProperty.Name == "Mechanism")
			{
				Mechanism mecha = (Mechanism)component.Properties["Mechanism"].Value;
				if (mecha != null)
				{
					int[] mechaAxes = mecha.GetActiveJoints();
					int iMechNumberOfAxes = mechaAxes.Length;
					component.Properties["MechSpecAxis"].Attributes["MaxValue"] = iMechNumberOfAxes.ToString();
				}
			}

			if (!bIsOnPropertyValueChanged)
			{
				//Update available controller
				UpdateControllers(component);
				//Save Modified Component for EventHandlers
				UpdateComponentList(component);
			}

			isOnPropertyValueChanged[component.UniqueId] = bIsOnPropertyValueChanged;
		}

		/// <summary>
		/// Called when the value of an I/O signal value has changed.
		/// </summary>
		/// <param name="component"> Component that owns the changed signal. </param>
		/// <param name="changedSignal"> Changed signal. </param>
		public override void OnIOSignalValueChanged(SmartComponent component, IOSignal changedSignal)
		{
			if (changedSignal.Name == "Update")
			{
				if ((int)changedSignal.Value == 1)
				{
					if (GetController(component) != null)
					{
						component.Properties["Status"].Value = "Updated";
					}
				}
			}

			UpdateComponentList(component);
		}

		/// <summary>
		/// Called during simulation.
		/// </summary>
		/// <param name="component"> Simulated component. </param>
		/// <param name="simulationTime"> Time (in ms) for the current simulation step. </param>
		/// <param name="previousTime"> Time (in ms) for the previous simulation step. </param>
		/// <remarks>
		/// For this method to be called, the component must be marked with
		/// simulate="true" in the xml file.
		/// </remarks>
		public override void OnSimulationStep(SmartComponent component, double simulationTime, double previousTime)
		{
			if ((bool)component.Properties["AllowUpdate"].Value)
				GetController(component);

			UpdateComponentList(component);
		}

		/// <summary>
		/// Called to retrieve the actual value of a property attribute with the dummy value '?'.
		/// </summary>
		/// <param name="component">Component that owns the property.</param>
		/// <param name="owningProperty">Property that owns the attribute.</param>
		/// <param name="attributeName">Name of the attribute to query.</param>
		/// <returns>Value of the attribute.</returns>
		public override string QueryPropertyAttributeValue(SmartComponent component, DynamicProperty owningProperty, string attributeName)
		{
			return "?";
		}

		/// <summary>
		/// Called to validate the value of a dynamic property with the CustomValidation attribute.
		/// </summary>
		/// <param name="component">Component that owns the changed property.</param>
		/// <param name="property">Property that owns the value to be validated.</param>
		/// <param name="newValue">Value to validate.</param>
		/// <returns>Result of the validation. </returns>
		public override ValueValidationInfo QueryPropertyValueValid(SmartComponent component, DynamicProperty property, object newValue)
		{
			return ValueValidationInfo.Valid;
		}


		//*********************************************************************************************
		/// <summary>
		/// Update internal component list to get them in EventHandler
		/// </summary>
		/// <param name="component">Component to update.</param>
		protected void UpdateComponentList(SmartComponent component)
		{
			bool bFound = false;
			for (int i = 0; i < myComponents.Count; ++i)
			{
				SmartComponent myComponent = myComponents[i];
				//Test if component exists as no OnUnLoad event exists.
				if ( (myComponent.ContainingProject == null)
					  || (myComponent.ContainingProject.GetObjectFromUniqueId(myComponents[i].UniqueId) == null)
						|| (myComponent.ContainingProject.Name == "")
						|| (bFound && (myComponent.UniqueId == component.UniqueId)) )
				{
					Logger.AddMessage("RSMoveMecha: Remove old Component " + myComponents[i].Name + " from cache.", LogMessageSeverity.Information);
					myComponents.Remove(myComponent);
					--i;
					continue;
				}
				if (myComponents[i].UniqueId == component.UniqueId)
				{
					myComponents[i] = component;
					bFound = true;
				}
			}
			if (!bFound)
				myComponents.Add(component);
		}

		/// <summary>
		/// Get all Controllers to update Allowed controllers.
		/// </summary>
		/// <param name="component">Component that owns the controller property.</param>
		protected void UpdateControllers(SmartComponent component)
		{
			if (!isUpdatingControllers.ContainsKey(component.UniqueId))
				isUpdatingControllers[component.UniqueId] = false;

			if (isUpdatingControllers[component.UniqueId])
				return;

			isUpdatingControllers[component.UniqueId] = true;

			string allowedValues = ";" + (string)component.Properties["Controller"].Value;
			if (allowedValues == ";Update") allowedValues = ";";
			string controllerId = allowedValues.Replace(";","");
			bool oldFound = false;

			foreach (ControllerObjectReference controllerObjectReference in ControllerManager.ControllerReferences)
			{
				if (!allowedValues.Contains(controllerObjectReference.SystemId.ToString()))
				{
					allowedValues += (";" + controllerObjectReference.SystemId.ToString() + " (" + controllerObjectReference.RobControllerConnection.ToString() + ")");
				} else
				{
					oldFound = true;
				}
			}
			allowedValues = allowedValues.Replace(";;", ";");
			if ((allowedValues == "") || (allowedValues == ";")) allowedValues = ";Update";
			component.Properties["Controller"].Attributes["AllowedValues"] = allowedValues;
			if (!oldFound && (controllerId != ""))
			{
				//Old controller not found test to connect to it.
				Logger.AddMessage("RSMoveMecha: Test Connecting Component " + component.Name + " to " + controllerId + ". Because it is not online.", LogMessageSeverity.Information);
				if (GetController(component) != null)
				{
					Logger.AddMessage("RSMoveMecha: Connecting Component " + component.Name + " to " + controllerId + ". But it is not online.", LogMessageSeverity.Information);
					component.Properties["Status"].Value = "Connected";
				}
				else
				{
					component.Properties["Status"].Value = "Disconnected";
				}
			}

			isUpdatingControllers[component.UniqueId] = false;
		}

		/// <summary>
		/// Get Controller from Controller property and axis values.
		/// </summary>
		/// <param name="component">Component that owns the changed property.</param>
		/// <param name="sTasksList">Tasks list delimited by ";".</param>
		/// <returns>Found Controller</returns>
		protected Controller GetController(SmartComponent component)
		{
			Controller controller = null;
			string guid = ((string)component.Properties["Controller"].Value).Split(' ')[0];
			if (guid != "")
			{
				Guid systemId = new Guid(guid);

				NetworkScanner scanner = new NetworkScanner();
				if (scanner.TryFind(systemId,TimeSpan.FromSeconds(60),1, out ControllerInfo controllerInfo))
				{
					controller = ControllerFactory.CreateFrom(controllerInfo);//new Controller(systemId);
					if (controller.SystemId == systemId)
					{
						if (controller.Connected)
						{
							int iCtrlMechCount = controller.MotionSystem.MechanicalUnits.Count;
							component.Properties["CtrlMechanism"].Attributes["MaxValue"] = iCtrlMechCount.ToString();
							if (iCtrlMechCount == 1)
							{
								component.Properties["CtrlMechanism"].UIVisible = false;
								component.Properties["CtrlMechanism"].Value = 1;
							}
							else
							{
								component.Properties["CtrlMechanism"].UIVisible = true;
							}

							try
							{
								UpdateAxis(component, controller);
							}
							catch (ABB.Robotics.GenericControllerException)
							{ }
						}
					}
				}
				else
				{
					//Try to connect controller later with OnControlerAdded
				}
			}
			else
			{
				component.Properties["Status"].Value = "Disconnected";
			}
			return controller;
		}

		/// <summary>
		///  Occurs before a ABB.Robotics.RobotStudio.Project is saved to file.
		/// </summary>
		/// <param name="sender">Sender</param>
		/// <param name="e">The event argument.</param>
		private void OnSaving(object sender, ABB.Robotics.RobotStudio.SavingProjectEventArgs e)
		{
			//Can't use foreach as collection is updated inside
			for (int i = 0; i < myComponents.Count; ++i)
			{
				SmartComponent myComponent = myComponents[i];
				//Test if component exists as no OnUnLoad event exists.
				if ( (myComponent.ContainingProject == null)
					  || (myComponent.ContainingProject.GetObjectFromUniqueId(myComponent.UniqueId) == null)
						|| (myComponent.ContainingProject.Name == "") )
				{
					Logger.AddMessage("RSMoveMecha: Remove old Component " + myComponent.Name + " from cache.", LogMessageSeverity.Information);
					myComponents.Remove(myComponent);
					--i;
					continue;
				}

				if (GetController(myComponent) != null)
					myComponent.Properties["Status"].Value = "Saved";
			}
		}

		/// <summary>
		///  Raised when a message is added.
		/// </summary>
		/// <param name="sender">Sender</param>
		/// <param name="e">The event argument.</param>
		private void OnLogMessageAdded(object sender, LogMessageAddedEventArgs e)
		{
			if (e.Message.Text.StartsWith("RSMoveMecha"))
				return;

			RobotStudio.API.Internal.EventLogMessage eventLogMessage = e.Message as RobotStudio.API.Internal.EventLogMessage;
			if ( (e.Message.Text.Contains("Update RSMoveMecha"))
				|| (e.Message.Text.StartsWith("Checked:") && e.Message.Text.EndsWith("No errors."))
				|| (e.Message.Text.StartsWith("Vérifié :") && e.Message.Text.EndsWith("aucune erreur."))
				|| (e.Message.Text.StartsWith("Geprüft:") && e.Message.Text.EndsWith("Keine Fehler."))
				|| (e.Message.Text.StartsWith("Comprobado:") && e.Message.Text.EndsWith("Sin errores."))
				|| (e.Message.Text.StartsWith("Verificato:") && e.Message.Text.EndsWith("Nessun errore."))
				|| (e.Message.Text.StartsWith("次の項目を確認しました:") && e.Message.Text.EndsWith("エラーはありません。"))
				|| (e.Message.Text.StartsWith("已检查：") && e.Message.Text.EndsWith("无错误。"))
				|| ((eventLogMessage != null) && (eventLogMessage.Msg.Domain == 1) && (eventLogMessage.Msg.Number == 122)) //Program stopped.
				//|| ((eventLogMessage != null) && (eventLogMessage.Msg.Domain == 1) && (eventLogMessage.Msg.Number == 123)) //Program stopped. Step
				|| ((eventLogMessage != null) && (eventLogMessage.Msg.Domain == 1) && (eventLogMessage.Msg.Number == 124)) //Program stopped. Start
				)
			{
				if ( DateTime.Compare(DateTime.UtcNow, lastUpdate.AddSeconds(1)) > 0 )
				{
					//Can't use foreach as collection is updated inside
					for (int i = 0; i < myComponents.Count; ++i)
					{
						SmartComponent myComponent = myComponents[i];
						//Test if component exists as no OnUnLoad event exists.
						if ((myComponent.ContainingProject == null)
								|| (myComponent.ContainingProject.GetObjectFromUniqueId(myComponent.UniqueId) == null)
								|| (myComponent.ContainingProject.Name == ""))
						{
							Logger.AddMessage("RSMoveMecha: Remove old Component " + myComponent.Name + " from cache.", LogMessageSeverity.Information);
							myComponents.Remove(myComponent);
							--i;
							continue;
						}

						Logger.AddMessage("RSMoveMecha: Updating Component " + myComponent.Name, LogMessageSeverity.Information);
						if (GetController(myComponent) != null)
						{
							myComponent.Properties["Status"].Value = "Updated" + System.DateTime.Now.ToString("yyyy-MM-dd_HH:mm:ss");
						}
					}
					lastUpdate = DateTime.UtcNow;
				}
			}
		}

		/// <summary>
		/// Raised when a controller reference is added.
		/// </summary>
		/// <param name="sender">Sender</param>
		/// <param name="e">The event argument.</param>
		public void OnControllerAdded(object sender, ControllerReferenceChangedEventArgs e)
		{
			//Can't use foreach as collection is updated inside
			for (int i = 0; i < myComponents.Count; ++i)
			{
				SmartComponent myComponent = myComponents[i];
				//Test if component exists as no OnUnLoad event exists.
				if ( (myComponent.ContainingProject == null)
					  || (myComponent.ContainingProject.GetObjectFromUniqueId(myComponent.UniqueId) == null)
						|| (myComponent.ContainingProject.Name == "") )
				{
					Logger.AddMessage("RSMoveMecha: Remove old Component " + myComponent.Name + " from cache.", LogMessageSeverity.Information);
					myComponents.Remove(myComponent);
					--i;
					continue;
				}

				string guid = ((string)myComponent.Properties["Controller"].Value).Split(' ')[0];
				if (guid != "")
				{
					Guid systemId = new Guid(guid);
					if (e.Controller.SystemId == systemId)
					{
						Logger.AddMessage("RSMoveMecha: Connecting Component " + myComponent.Name, LogMessageSeverity.Information);
						if (GetController(myComponent) != null)
							myComponent.Properties["Status"].Value = "Connected";

					}
				}
				string allowedValues = (string)myComponent.Properties["Controller"].Attributes["AllowedValues"].ToString();
				if (!allowedValues.Contains(e.Controller.SystemId.ToString()))
				{
					allowedValues += (";" + e.Controller.SystemId.ToString() + " (" + e.Controller.RobControllerConnection.ToString() + ")");
				}
				if (allowedValues == "") allowedValues = ";Update";
				myComponent.Properties["Controller"].Attributes["AllowedValues"] = allowedValues;
			}
		}

		/// <summary>
		/// Raised when a controller reference is removed.
		/// </summary>
		/// <param name="sender">Sender</param>
		/// <param name="e">The event argument.</param>
		public void OnControllerRemoved(object sender, ControllerReferenceChangedEventArgs e)
		{
			//Can't use foreach as collection is updated inside
			for (int i = 0; i < myComponents.Count; ++i)
			{
				SmartComponent myComponent = myComponents[i];
				//Test if component exists as no OnUnLoad event exists.
				if ( (myComponent.ContainingProject == null)
					  || (myComponent.ContainingProject.GetObjectFromUniqueId(myComponent.UniqueId) == null)
						|| (myComponent.ContainingProject.Name == "") )
				{
					Logger.AddMessage("RSMoveMecha: Remove old Component " + myComponent.Name + " from cache.", LogMessageSeverity.Information);
					myComponents.Remove(myComponent);
					--i;
					continue;
				}

				string guid = ((string)myComponent.Properties["Controller"].Value).Split(' ')[0];
				if (guid != "")
				{
					Guid systemId = new Guid(guid);
					if (e.Controller.SystemId == systemId)
					{
						Logger.AddMessage("RSMoveMecha: Disconnecting Component " + myComponent.Name, LogMessageSeverity.Information);
						myComponent.Properties["Status"].Value = "Disconnected";
					}
				}
			}
		}

		/// <summary>
		/// Update Mechanism axes values
		/// </summary>
		/// <param name="component">Component that owns signals. </param>
		/// <param name="jtValue">JointTarget Value</param>
		private void UpdateAxis(SmartComponent component, Controller controller)
		{
			int iCtrlMechanismIndex = (int)component.Properties["CtrlMechanism"].Value;
			if ((iCtrlMechanismIndex<1) || (iCtrlMechanismIndex>controller.MotionSystem.MechanicalUnits.Count))
				component.Properties["CtrlMechanism"].Value = iCtrlMechanismIndex = 1;

			//Get Controller Values
			ABB.Robotics.Controllers.MotionDomain.MechanicalUnit mu = controller.MotionSystem.MechanicalUnits[iCtrlMechanismIndex - 1];
			int iCtrlNumberOfAxes = mu.NumberOfAxes;
			JointTarget jtValue = mu.GetPosition();
			component.Properties["CtrlSpecAxis"].Attributes["MaxValue"] = iCtrlNumberOfAxes.ToString();
			int iCtrlSpecAxis = (int)component.Properties["CtrlSpecAxis"].Value;
			if (iCtrlSpecAxis > iCtrlNumberOfAxes)
				component.Properties["CtrlSpecAxis"].Value = iCtrlSpecAxis = iCtrlNumberOfAxes;
			if (iCtrlSpecAxis < 0)
				component.Properties["CtrlSpecAxis"].Value = iCtrlSpecAxis = 0;

			//Get Mechanism Values
			Mechanism mecha = (Mechanism)component.Properties["Mechanism"].Value;
			if (mecha == null) return;
			int[] mechaAxes = mecha.GetActiveJoints();
			int iMechNumberOfAxes = mechaAxes.Length;
			double[] mechaValues = mecha.GetJointValues();
			component.Properties["MechSpecAxis"].Attributes["MaxValue"] = iMechNumberOfAxes.ToString();
			int iMechSpecAxis = (int)component.Properties["MechSpecAxis"].Value;
			if (iMechSpecAxis > iCtrlNumberOfAxes)
				component.Properties["MechSpecAxis"].Value = iMechSpecAxis = iMechNumberOfAxes;
			if (iMechSpecAxis < 0)
				component.Properties["MechSpecAxis"].Value = iMechSpecAxis = 0;

			//Start Updating
			int iAxesUpdated = 0;
			if (iCtrlSpecAxis == 0)
			{
				if (iMechSpecAxis == 0)
				{ //Take all Controller axes to update all Mechanism
					for (int iAxis = 1; (iAxis <= iCtrlNumberOfAxes) && (iAxis <= iMechNumberOfAxes); ++iAxis)
						mechaValues[iAxis - 1] = GetJointTargetAxis(jtValue, iAxis, mu.Type);
				}
				else
				{ //Only Take Mechanism Specific axis from controller to update it
					if ((iMechSpecAxis <= iCtrlNumberOfAxes) && (iMechSpecAxis <= iMechNumberOfAxes))
						mechaValues[iMechSpecAxis-1] = GetJointTargetAxis(jtValue, iMechSpecAxis, mu.Type);
				}
			}
			else
			{
				if (iMechSpecAxis == 0)
				{ //Only Take Controller Specific axis to update same in Mechanism
					if ((iCtrlSpecAxis <= iCtrlNumberOfAxes) && (iCtrlSpecAxis <= iMechNumberOfAxes))
						mechaValues[iCtrlSpecAxis - 1] = GetJointTargetAxis(jtValue, iCtrlSpecAxis, mu.Type);
				}
				else
				{ //Only Take Controller Specific axis to update Mechanism Specific axis
					if ((iCtrlSpecAxis <= iCtrlNumberOfAxes) && (iMechSpecAxis <= iMechNumberOfAxes))
						mechaValues[iMechSpecAxis-1] = GetJointTargetAxis(jtValue, iCtrlSpecAxis, mu.Type);
				}
			}


			iAxesUpdated =((iCtrlSpecAxis == 0) && (iMechSpecAxis == 0)) ? Math.Min(iCtrlNumberOfAxes, iMechNumberOfAxes) : 1;

			//Updating
			if (!mecha.SetJointValues(mechaValues, true))
			{
				component.Properties["Status"].Value = "Error";
				Logger.AddMessage("RSMoveMecha: Component " + component.Name + " can't update " + mecha.Name + ".", LogMessageSeverity.Error);
			}
			else
			{
				string sNumberOfAxesStatus = iAxesUpdated.ToString() + "/" + iMechNumberOfAxes.ToString() + " Mechanism axes updated from " + iCtrlNumberOfAxes.ToString() +" Controller axes.";
				component.Properties["NumberOfAxesStatus"].Value = sNumberOfAxesStatus;
			}
		}

		/// <summary>
		/// Get JointTarget Axis Value.
		/// </summary>
		/// <param name="jtValue">JointTarget with Values</param>
		/// <param name="axis">Axis number</param>
		/// <param name="mut">MechanicalUnitType to know if robot or other.</param>
		/// <returns></returns>
		private double GetJointTargetAxis(JointTarget jtValue, int axis, ABB.Robotics.Controllers.MotionDomain.MechanicalUnitType mut = ABB.Robotics.Controllers.MotionDomain.MechanicalUnitType.TcpRobot)
		{
			switch (axis)
			{
				case 1:
					switch (mut)
					{
						case ABB.Robotics.Controllers.MotionDomain.MechanicalUnitType.TcpRobot:
							return ((double)jtValue.RobAx.Rax_1 / 360) * 2 * Math.PI;
						case ABB.Robotics.Controllers.MotionDomain.MechanicalUnitType.SingleAxis:
							return (double)jtValue.ExtAx.Eax_a / 1000;
						default:
							Logger.AddMessage("RSMoveMecha: GetJointTargetAxis for unmanaged MechanicalUnitType. Returns 0.", LogMessageSeverity.Error);
							return 0;
					}
				case 2: return ((double)jtValue.RobAx.Rax_2 / 360) * 2 * Math.PI;
				case 3: return ((double)jtValue.RobAx.Rax_3 / 360) * 2 * Math.PI;
				case 4: return ((double)jtValue.RobAx.Rax_4 / 360) * 2 * Math.PI;
				case 5: return ((double)jtValue.RobAx.Rax_5 / 360) * 2 * Math.PI;
				case 6: return ((double)jtValue.RobAx.Rax_6 / 360) * 2 * Math.PI;
				case 7: return ((double)jtValue.ExtAx.Eax_a / 360) * 2 * Math.PI;
				case 8: return ((double)jtValue.ExtAx.Eax_b / 360) * 2 * Math.PI;
				case 9: return ((double)jtValue.ExtAx.Eax_c / 360) * 2 * Math.PI;
				case 10: return ((double)jtValue.ExtAx.Eax_d / 360) * 2 * Math.PI;
				case 11: return ((double)jtValue.ExtAx.Eax_e / 360) * 2 * Math.PI;
				case 12: return ((double)jtValue.ExtAx.Eax_f / 360) * 2 * Math.PI;
				default: return 0;
			}
		}

	}
}
