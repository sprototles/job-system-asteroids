# job-system-asteroids
Demo for comparing Unity main thread vs job system

This demo is simulation of n-object in zero-G space, where every object gravity interact with each other
Main purpose of this is to show difference between calculations in main thread and using Unity.Jobs and Unity.Burst system
and also as an example for newbies, if they want to try job system

In this demo you can see performance increase from 300% to 500% using Unity.Jobs vs main thread.

There are 4 options to run simulation
1. main thread
2. Job
3. JobFor
4. JobParallerFor

you can see some example Unity.Jobs [here](https://docs.unity3d.com/ScriptReference/Unity.Jobs.IJob.html)  

Feel free to suggest some improvements if you can see some.
