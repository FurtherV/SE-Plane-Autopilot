using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using System;
using System.Collections.Generic;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        private const double TIMES_STEP = 10.0 / 60.0;

        private const string ELEVATOR_GROUP_NAME = "Elevators";
        private const string AILERON_GROUP_NAME = "Ailerons";
        private const string RUDDER_GROUP_NAME = "Rudders";

        private IMyShipController _primaryCockpit = null;
        private readonly List<IMyShipController> _foundCockpits = new List<IMyShipController>();

        private readonly List<IMyTerminalBlock> _elevators = new List<IMyTerminalBlock>();
        private readonly List<IMyTerminalBlock> _ailerons = new List<IMyTerminalBlock>();
        private readonly List<IMyTerminalBlock> _rudders = new List<IMyTerminalBlock>();

        private readonly PID _pidPitch = new PID(5, 0, 0, TIMES_STEP);
        private readonly PID _pidRoll = new PID(5, 0, 0, TIMES_STEP);
        private readonly PID _pidBearing = new PID(5, 0, 0, TIMES_STEP);

        private double? _desiredPitch = null;
        private double? _desiredRoll = null;
        private double? _desiredBearing = null;

        private bool _initialized = false;
        private MyCommandLine _myCommandLine = new MyCommandLine();
        private bool _enabled = false;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        public void Save()
        {

        }

        public void Main(string argument, UpdateType updateSource)
        {
            Setup();

            if ((updateSource & UpdateType.Update10) != 0)
            {
                if (_primaryCockpit == null)
                {
                    Echo("Error: Could not find a cockpit block!");
                    return;
                }

                if (_elevators.Count == 0)
                {
                    Echo("Error: Could not find any elevators!");
                    return;
                }

                if (_ailerons.Count == 0)
                {
                    Echo("Error: Could not find any ailerons!");
                    return;
                }

                if (_rudders.Count == 0)
                {
                    Echo("Error: Could not find any rudders!");
                    return;
                }

                if (!_enabled)
                {
                    Echo("Status: Disabled");
                    return;
                }
                Echo("Status: Enabled");

                double pitch = GetCurrentPitch(_primaryCockpit);
                double roll = GetCurrentRoll(_primaryCockpit);
                double bearing = GetCurrentBearing(_primaryCockpit);

                float pitchControl = 0f;
                float rollControl = 0f;
                float bearingControl = 0f;

                if (_desiredPitch.HasValue)
                {
                    double error = _desiredPitch.Value - pitch;
                    pitchControl = (float)_pidPitch.Control(error);
                }

                if (_desiredRoll.HasValue)
                {
                    double error = _desiredRoll.Value - roll;
                    rollControl = (float)_pidRoll.Control(error);
                }

                if (_desiredBearing.HasValue)
                {
                    double error = _desiredBearing.Value - bearing;
                    double normalized_error = error - 360 * Math.Floor((error + 180) / 360);
                    bearingControl = (float)_pidBearing.Control(-normalized_error);
                }

                ApplyTrimMultiple(_elevators, pitchControl, "InvertPitch");
                ApplyTrimMultiple(_ailerons, rollControl, "Invert"); // For some reason the expected InvertRoll is just Invert...
                ApplyTrimMultiple(_rudders, bearingControl, "InvertYaw");
                Echo($"Pitch: C:{pitch} T:{_desiredPitch} PID:{pitchControl}");
                Echo($"Roll: C:{roll} T:{_desiredRoll} PID:{rollControl}");
                Echo($"Bearing: C:{bearing} T:{_desiredBearing} PID:{bearingControl}");
                return;
            }

            // Not run by update, process possible commands
            if (!_myCommandLine.TryParse(argument) || _myCommandLine.ArgumentCount == 0)
                return;

            string subCommand = _myCommandLine.Argument(0).ToLowerInvariant();

            switch (subCommand)
            {
                case "start":
                case "on":
                    _enabled = true;
                    break;
                case "stop":
                case "off":
                    _enabled = false;
                    ApplyTrimMultiple(_elevators, 0, "InvertPitch");
                    ApplyTrimMultiple(_ailerons, 0, "InvertRoll");
                    ApplyTrimMultiple(_rudders, 0, "InvertYaw");
                    break;
                case "refresh":
                    _initialized = false;
                    break;
                case "set":
                case "add":
                case "sub":
                case "reset":
                    UpdateDesired(subCommand, _myCommandLine.Argument(1), _myCommandLine.Argument(2));
                    break;
                case "debug":
                    IMyTerminalBlock wing = _elevators[0];
                    Me.CustomData = "";
                    wing.GetProperties(null, (prop) =>
                    {
                        Me.CustomData += $"{prop.Id}:{prop.TypeName}\n";
                        return false;
                    });
                    break;
            }
        }

        private void UpdateDesired(string mode, string type, string valueStr)
        {
            if (string.IsNullOrEmpty(mode) || string.IsNullOrEmpty(type))
                return;

            switch (mode.ToLowerInvariant())
            {
                case "reset":
                    if (type.ToLowerInvariant() == "all")
                    {
                        UpdateDesiredInternal("pitch", null, false);
                        UpdateDesiredInternal("roll", null, false);
                        UpdateDesiredInternal("bearing", null, false);
                        break;
                    }
                    UpdateDesiredInternal(type, null, false);
                    break;
                case "set":
                case "add":
                case "sub":
                    if (string.IsNullOrEmpty(valueStr))
                        return;

                    double value;
                    if (!double.TryParse(valueStr, out value))
                        return;

                    bool isOffset = true;
                    switch (mode.ToLowerInvariant())
                    {
                        case "set":
                            isOffset = false;
                            break;
                        case "sub":
                            value *= -1;
                            break;
                    }

                    UpdateDesiredInternal(type, value, isOffset);
                    break;
            }
        }

        private void UpdateDesiredInternal(string type, double? value, bool isOffset)
        {
            switch (type.ToLowerInvariant())
            {
                case "pitch":
                    if (isOffset && value.HasValue)
                    {
                        _desiredPitch = _desiredPitch.HasValue ? _desiredPitch + value : value;
                        break;
                    }
                    _desiredPitch = value;
                    break;
                case "roll":
                    if (isOffset && value.HasValue)
                    {
                        _desiredRoll = _desiredRoll.HasValue ? _desiredRoll + value : value;
                        break;
                    }
                    _desiredRoll = value;
                    break;
                case "bearing":
                    if (isOffset && value.HasValue)
                    {
                        _desiredBearing = _desiredBearing.HasValue ? _desiredBearing + value : value;
                        break;
                    }
                    _desiredBearing = value;
                    break;
            }
        }

        private void Setup()
        {
            if (_initialized)
                return;

            _initialized = true;

            FindBlocks();
        }

        private void FindBlocks()
        {
            _foundCockpits.Clear();
            GridTerminalSystem.GetBlocksOfType<IMyShipController>(_foundCockpits);
            if (_foundCockpits.Count == 0)
                return;

            // Main Cockpit > Controlled Cockpit > First Cockpit
            _primaryCockpit = _foundCockpits[0];
            foreach (var cockpit in _foundCockpits)
            {
                if (cockpit.IsMainCockpit)
                {
                    _primaryCockpit = cockpit;
                    break;
                }

                if (cockpit.IsUnderControl)
                {
                    _primaryCockpit = cockpit;
                    break;
                }
            }

            _elevators.Clear();
            _ailerons.Clear();
            _rudders.Clear();

            IMyBlockGroup elevatorGroup = GridTerminalSystem.GetBlockGroupWithName(ELEVATOR_GROUP_NAME);
            if (elevatorGroup == null)
                return;
            elevatorGroup.GetBlocksOfType<IMyTerminalBlock>(_elevators, IsWing);

            IMyBlockGroup aileronGroup = GridTerminalSystem.GetBlockGroupWithName(AILERON_GROUP_NAME);
            if (aileronGroup == null)
                return;
            aileronGroup.GetBlocksOfType<IMyTerminalBlock>(_ailerons, IsWing);

            IMyBlockGroup rudderGroup = GridTerminalSystem.GetBlockGroupWithName(RUDDER_GROUP_NAME);
            if (rudderGroup == null)
                return;
            rudderGroup.GetBlocksOfType<IMyTerminalBlock>(_rudders, IsWing);
        }

        private double GetCurrentPitch(IMyShipController shipController)
        {
            Vector3D up = -shipController.GetNaturalGravity();
            Vector3D left = Vector3D.Cross(up, shipController.WorldMatrix.Forward);
            Vector3D forward = Vector3D.Cross(left, up);

            double pitch = VectorMath.AngleBetween(forward, shipController.WorldMatrix.Forward) * Math.Sign(Vector3D.Dot(up, shipController.WorldMatrix.Forward));
            return Math.Round(MathHelper.ToDegrees(pitch), 2);


        }

        private double GetCurrentRoll(IMyShipController shipController)
        {
            Vector3D up = -shipController.GetNaturalGravity();
            Vector3D left = Vector3D.Cross(up, shipController.WorldMatrix.Forward);
            Vector3D forward = Vector3D.Cross(left, up);

            Vector3D localUpVector = Vector3D.TransformNormal(up, MatrixD.Transpose(shipController.WorldMatrix));
            Vector3D flattenedUpVector = new Vector3D(localUpVector.X, localUpVector.Y, 0); // Note: +X is Right, +Y is Up and +Z is Backwards

            double roll = VectorMath.AngleBetween(flattenedUpVector, Vector3D.Up) * Math.Sign(Vector3D.Dot(Vector3D.Right, flattenedUpVector));
            return Math.Round(MathHelper.ToDegrees(roll), 2);
        }

        private double GetCurrentBearing(IMyShipController shipController)
        {
            Vector3D gravity = shipController.GetNaturalGravity();
            Vector3D eastVec = Vector3D.Cross(gravity, new Vector3D(0, -1, 0));
            Vector3D northVec = Vector3D.Cross(eastVec, gravity);
            Vector3D heading = VectorMath.Rejection(shipController.WorldMatrix.Forward, gravity);

            double bearing = MathHelper.ToDegrees(VectorMath.AngleBetween(heading, northVec));
            if (Vector3D.Dot(shipController.WorldMatrix.Forward, eastVec) < 0)
                bearing = 360 - bearing;

            if (bearing >= 359.5)
                bearing = 0;

            return Math.Round(bearing, 2);
        }

        #region Wing Functions

        private bool IsWing(IMyTerminalBlock block)
        {
            if (block == null)
                return false;

            ITerminalProperty prop = block.GetProperty("Draygo.ControlSurface.Trim");
            return prop != null;
        }

        private void ApplyTrimMultiple(List<IMyTerminalBlock> wings, float trim, string invertName)
        {
            if (wings == null || wings.Count == 0)
                return;

            foreach (var wing in wings)
                ApplyTrim(wing, trim, invertName);
        }

        private void ApplyTrim(IMyTerminalBlock wing, float trim, string invertName)
        {
            if (float.IsNaN(trim) || float.IsInfinity(trim))
                return;

            trim = Math.Max(-44, Math.Min(44, trim));

            ITerminalProperty<float> trimProp = wing.GetProperty("Draygo.ControlSurface.Trim").AsFloat();

            if (trim != 0)
            {
                ITerminalProperty<bool> invertPitchProp = wing.GetProperty($"Draygo.ControlSurface.{invertName}").AsBool();
                bool invert = invertPitchProp.GetValue(wing);
                if (invert)
                    trim *= -1;
            }

            trimProp.SetValue(wing, trim);
        }

        #endregion
    }
}
