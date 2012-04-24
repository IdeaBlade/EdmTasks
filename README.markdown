EdmTasks
========
MSBuild tasks for working with Entity Data Models

Introduction
------------
EdmTasks is a set of MSBuild tasks for working with entity data models, 
particularly for creating the [pre-generated views][pgv] file to 
improve startup performance.

While there are other excellent tools for this, such as [EF Power Tools][efp]
and [EdmGen2][eg2], EdmTasks integrates view generation with your build. 
When your model changes, your pre-generated views are automatically updated.

[pgv]: http://msdn.microsoft.com/en-us/library/bb896240.aspx
[efp]: http://blogs.msdn.com/b/adonet/archive/2012/04/09/ef-power-tools-beta-2-available.aspx
[eg2]: http://archive.msdn.microsoft.com/EdmGen2

There are three tasks defined:
- EdmxWriteTask - writes an EDMX file from a Code-First DbContext
- ViewGenTask - writes a pre-generated views file from an EDMX file
- ViewRefreshTask - writes a pre-generated views file from a DbContext, but only if the model has changed.

For projects that have a `DbContext`, ViewRefreshTask is the one you will want to use,
since it automatically updates your views as needed.  It will be used as an example below.

Building EdmTasks
-----------------
When you download the EdmTasks solution, there will be a broken reference to EntityFramework.
You will need to remove this reference and replace it with a reference to the EntityFramework
assembly that you will use in your builds.  It is important that the version of EF used to
build EdmTasks is the same as the one you will use in your business EDM solution.

Once you have corrected the EF reference, you can build the EdmTasks solution.  Then,
copy the DLL files from the EdmTasks\bin\Debug folder to another location where it 
can be used by your EDM solution's build.

Please note that you cannot put the EdmTasks project in the same solution where the tasks
will be run.  The EdmTasks.dll assembly will be locked, and you won't be able to update it
again.  So it is best to build EdmTasks, then copy EdmTasks.dll and EntityFramework.dll 
to a separate directory where it can be called during the build of your EDM solution.

Building Your Solution
----------------------
Once the DLLs are deployed, you will need to update your domain model project file, 
e.g. MyDomainModel.csproj.  Add the ViewRefreshTask in the BeforeBuild target; you can 
do this at the bottom of the file, right before the closing `</project>`.  You can
copy and paste the code below, then update the path to EdmTasks.dll to match your system.

	  <!-- Define the task and the DLL where it is found.  -->
	  <!-- EdmTasks.dll, ConceptualEdmGen.dll, and EntityFramework.dll should be in the same directory. -->
	  <UsingTask TaskName="EdmTasks.ViewRefreshTask" AssemblyFile="..\..\Tools\EdmTasks\EdmTasks.dll" />

	  <!-- Run BeforeBuild only if the Compile list (*.cs) is newer than the TargetPath (dll) -->
	  <Target Name="BeforeBuild" Inputs="@(Compile)" Outputs="$(TargetPath)">

	    <!-- CoreBuild the project first so we have the model DLL. -->
	    <MSBuild Projects="$(MSBuildProjectFullPath)" Targets="CoreBuild" />

	    <!-- Find the DbContext in the TargetPath assembly, use it to generate the views. -->
	    <ViewRefreshTask Assembly="$(TargetPath)" Lang="cs" />

	  </Target>

Note that the task builds the DbContext.Views.cs file, but it does not add the file to the project.
The first time you run it, you will need to add it to the project if it is not already added.

How It Works
------------
ViewRefreshTask relies on Entity Framework's own EdmxWriter and EntityViewGenerator classes, so the
results are consistent with what EF itself and other tools can produce.  The basic steps are:

1. Find a DbContext from the given assembly name.
2. Create an EDMX in memory from the DbContext.
3. Create a 256-bit hash of the EDMX.
4. Compare the hash to the one at on the first line of the old Views file (if it exists).
5. If the hashes are different, build a new Views file (with the new hash on the first line).

Performance
-----------
ViewRefreshTask was tested primarily on a 200-entity model.  The timings were roughly as follows:
- No change in domain model: 0 seconds (the BeforeBuild target parameters ensure that the task does not run in this situation)
- Change in the domain model that does not affect the views: 5 seconds (the EDMX has not changed, so Views file is not regenerated)
- Change in the domain model that does affect the views: 60 seconds (a new Vies file is regenerated)

Caveats
-------
The algorithm that ViewRefreshTasks uses to detect changes in the model is not the same as the 
internal one used by Entity Framework.  So there may be cases when ViewRefreshTask does not rebuild 
the model when it should.  If this happens, EF will give you an error the first time you use it,
so you'll know you need to re-generated the views.

To force the views to be re-generated, just open the file (MyDbContext.Views.cs or whatever) and delete
the first line of the file, which begins `// ViewGenHash=`.  This will force the views to be re-generated the
next time the task runs.

Tested Versions
------------------
EdmTasks was tested with Visual Studio 2010, using Entity Framework 4.1, 4.2, and 4.3.1.  
It may work on other versions as well.

