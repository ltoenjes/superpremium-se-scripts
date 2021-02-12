const long DIRK_ADDR = 131852551918680347;
public string[] states = new string[] { "idle", "reqDocking", "waitForAnswer", "dockingProcedure", "dockingLastRotation", "finishedDocking" };

public Program()

{

    Runtime.UpdateFrequency = UpdateFrequency.Update1;

}

public void Main(string argument, UpdateType updateSource)
{
    IMyRemoteControl remote = (IMyRemoteControl)GridTerminalSystem.GetBlockWithName("Remote");
    IMyTerminalBlock blockRef1 = GridTerminalSystem.GetBlockWithName("Connector1");
    IMyTerminalBlock blockRef2 = GridTerminalSystem.GetBlockWithName("Connector2");
    IMyGyro gyro = (IMyGyro)GridTerminalSystem.GetBlockWithName("Gyroscope");
    //Echo(IGC.Me.ToString());
    //Echo(DateTime.Now.ToString());

    Echo(Me.CustomData);

    var data = Me.CustomData.Split('|');

    string state = data[0];
    string lastStateChange = data.Length > 1 ? data[1] : string.Empty;
    string vectorData = data.Length > 2 ? data[2] : string.Empty;
    string eulerData = data.Length > 3 ? data[3] : string.Empty;
    Echo("State:" + state);

    if (!states.Contains(state))
    {
        state = "idle";
    }

    switch (state)
    {
        case ("idle"):
            {
                state = "reqDocking";
                IGC.SendUnicastMessage<string>(DIRK_ADDR, "Test", "Hallo Welt!");
                state = "waitForAnswer";
                break;
            }

        case ("waitForAnswer"):
            {
                IMyUnicastListener unisource = IGC.UnicastListener;
                if (unisource.HasPendingMessage)
                {
                    MyIGCMessage message = unisource.AcceptMessage();
                    Echo("Received message: " + message.Data.ToString());
                    vectorData = message.Data.ToString();
                    state = "dockingProcedure";
                }

                break;
            }

        case ("dockingProcedure"):
            {
                eulerData = string.Empty;
                if (!remote.IsAutoPilotEnabled)
                {
                    var vectorStr = vectorData.Split(';').Select(v => v.Split(','));
                    var vectorMessages = vectorStr.Select(v => v[0]);
                    var vectors = vectorStr.Select(v => new Vector3(double.Parse(v[1]), double.Parse(v[2]), double.Parse(v[3]))).Reverse();

                    if (vectors.Count() == 1)
                    {
                        state = "dockingLastRotation";
                        break;
                    }

                    //foreach(var v in vectors)
                    var vector = vectors.First();
                    {
                        Echo("Target: " + vector.X.ToString() + ";" + vector.Y.ToString() + ";" + vector.Z.ToString());

                        remote.ClearWaypoints();
                        remote.WaitForFreeWay = false;
                        remote.AddWaypoint(vector, "Target");
                        remote.SetCollisionAvoidance(false);
                        remote.SetAutoPilotEnabled(true);

                        //Remove point after it was added to the waypoints
                        vectorData = string.Join(";", vectorData.Split(';').Take(1));
                    }
                }

                break;
            }

        case ("dockingLastRotation"):
            {

                if (string.IsNullOrEmpty(eulerData))
                {
                    // Initial calculation
                    Echo("Initial calculation");
                    var vectorStr = vectorData.Split(';').Select(v => v.Split(','));
                    var vectorMessages = vectorStr.Select(v => v[0]);
                    var vectors = vectorStr.Select(v => new Vector3(double.Parse(v[1]), double.Parse(v[2]), double.Parse(v[3])));

                    Vector3 targetPos = vectors.First();
                    Vector3 sourcePos = remote.CenterOfMass;

                    Vector3 direction = targetPos - sourcePos;
                    Vector3 direction2 = direction + new Vector3(1.0, 0.0, 0.0);

                    Vector3 orth = Vector3.Cross(direction, direction2);
                    Quaternion targetQuaternion = Quaternion.CreateFromForwardUp(Vector3.Normalize(direction), Vector3.Normalize(orth));

                    var blockDir = blockRef1.GetPosition() - blockRef2.GetPosition();
                    var blockDir2 = blockDir + new Vector3(1.0, 0.0, 0.0);

                    var blockOrth = Vector3.Cross(blockDir, blockDir2);
                    var currentQuaternion = Quaternion.CreateFromForwardUp(Vector3.Normalize(blockDir), Vector3.Normalize(blockOrth));

                    var currentEuler = ToEulerAngles(currentQuaternion);
                    var targetEuler = ToEulerAngles(targetQuaternion);

                    eulerData = "-"+string.Join(",", currentEuler) + ";" + string.Join(",", targetEuler);
                }
                else
                {
                    // Just update current rotation
                    var blockDir = blockRef1.GetPosition() - blockRef2.GetPosition();
                    var blockDir2 = blockDir + new Vector3(1.0, 0.0, 0.0);

                    var blockOrth = Vector3.Cross(blockDir, blockDir2);
                    var currentQuaternion = Quaternion.CreateFromForwardUp(Vector3.Normalize(blockDir), Vector3.Normalize(blockOrth));

                    var currentEuler = ToEulerAngles(currentQuaternion);

                    eulerData = eulerData[0].ToString()+string.Join(",", currentEuler) + ";" + eulerData.Split(';').Last();
                }

                var eulerDataStrings = eulerData.Substring(1).Split(';').Select(x => x.Split(',')).ToArray();

                double rollOffset = double.Parse(eulerDataStrings[0][0]) - double.Parse(eulerDataStrings[1][0]);
                double pitchOffset = double.Parse(eulerDataStrings[0][1]) - double.Parse(eulerDataStrings[1][1]);
                double yawOffset = double.Parse(eulerDataStrings[0][2]) - double.Parse(eulerDataStrings[1][2]);

                double[] offsets = { rollOffset, pitchOffset, yawOffset };
                float[] speeds = { 0.0f, 0.0f, 0.0f };

                Echo($"Roll={rollOffset.ToString()},Pitch={pitchOffset.ToString()},Yaw={yawOffset.ToString()}");

                var availableAxesCount = offsets.Where(x => Math.Abs(x) > 0.05).Count();
                int random = new Random().Next(0, availableAxesCount);
                Echo(availableAxesCount.ToString());

                Echo(DateTime.UtcNow.Subtract(Convert.ToDateTime(lastStateChange)).ToString());
                if(eulerData[0] == '-' || DateTime.UtcNow.Subtract(Convert.ToDateTime(lastStateChange)) > new TimeSpan(0,0,10))
                {
                    gyro.Yaw = speeds[0];
                    gyro.Roll = speeds[1];
                    gyro.Pitch = speeds[2];

                    int index = offsets.ToList().IndexOf(offsets.Where(x => Math.Abs(x) > 0.05).ToArray()[random]);

                    if (eulerData[0] != '-')
                    {
                        lastStateChange = DateTime.UtcNow.ToString();
                    }

                    eulerData = index.ToString() + eulerData.Substring(1);

                    // Select random angle and rotate
                    if (offsets.Max() <= 0.05)
                    {
                        gyro.GyroOverride = false;
                        state = "dockingLastTranslation";
                    }
                    break;
                }
                else
                {
                    gyro.GyroOverride = true;

                    int i = int.Parse(eulerData[0].ToString());

                    if (offsets[i] > 0.05)
                    {
                        speeds[i] = (float)(Math.Sign(offsets[i]) * 0.3);

                        gyro.Yaw = speeds[0];
                        gyro.Roll = speeds[1];
                        gyro.Pitch = speeds[2];
                    }
                    else
                    {
                        eulerData = "-" + eulerData.Substring(1);
                        break;
                    }
                }

                Echo(string.Join(",", speeds.Select(speed => speed.ToString("0.00"))));

                break;
            }

        case ("dockingLastTranslation"):
            {
                // Assumption: it's enough when you calculate docking port and mass center as offset and calculate the direction downwards
                var worldCOF = remote.CenterOfMass;
                var worldDock = blockRef1.GetPosition();

                var vectorStr = vectorData.Split(';').Select(v => v.Split(','));
                var vectorMessages = vectorStr.Select(v => v[0]);
                var vectors = vectorStr.Select(v => new Vector3(double.Parse(v[1]), double.Parse(v[2]), double.Parse(v[3])));

                Vector3 targetPos = vectors.First();

                // Current position = center of mass
                // Target position = world dock + target pos

                var targetDirection = ((targetPos + worldDock) - worldCOF).Normalize();

                List<IMyThrust> thrusters = new List<IMyThrust>();
                GridTerminalSystem.GetBlocksOfType<IMyThrust>(thrusters);

                foreach (IMyThrust thruster in thrusters)
                {
                    Echo(thruster.GridThrustDirection.ToString());
                }
                

                Echo($"Thrusters: {thrusters.Count()}");
                break;
            }

        case ("finishedDocking"):
            {
                Echo("finished docking!!! <3");
                break;
            }
    }

    if (state != data[0])
    {
        lastStateChange = DateTime.UtcNow.ToString();
    }

    Me.CustomData = state + "|" + lastStateChange + "|" + vectorData + "|" + eulerData;
}

List<double> ToEulerAngles(Quaternion q)
{
    // roll pitch yaw
    List<double> angles = new List<double>() { 0.0, 0.0, 0.0 };

    // roll (x-axis rotation)
    double sinr_cosp = +2.0 * (q.W * q.X + q.Y * q.Z);
    double cosr_cosp = +1.0 - 2.0 * (q.X * q.X + q.Y * q.Y);
    angles[0] = Math.Atan2(sinr_cosp, cosr_cosp);

    // pitch (y-axis rotation)
    double sinp = +2.0 * (q.W * q.Y - q.Z * q.X);
    if (Math.Abs(sinp) >= 1)
        angles[1] = Math.PI / 2 * Math.Sign(sinp); // use 90 degrees if out of range
    else
        angles[1] = Math.Asin(sinp);

    // yaw (z-axis rotation)
    double siny_cosp = +2.0 * (q.W * q.Z + q.X * q.Y);
    double cosy_cosp = +1.0 - 2.0 * (q.Y * q.Y + q.Z * q.Z);
    angles[2] = Math.Atan2(siny_cosp, cosy_cosp);

    return angles;
}