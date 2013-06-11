﻿using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Windows.Forms;
using Chorus.FileTypeHanders.lift;
using Chorus.UI.Sync;
using Chorus.sync;
using SIL.LiftBridge.Properties;
using TriboroughBridge_ChorusPlugin;
using TriboroughBridge_ChorusPlugin.Infrastructure;

namespace SIL.LiftBridge.Infrastructure.ActionHandlers
{
	/// <summary>
	/// This IBridgeActionTypeHandler implementation handles everything needed for a normal S/R for a Lift repo.
	/// </summary>
	[Export(typeof(IBridgeActionTypeHandler))]
	internal sealed class SendReceiveLiftActionHandler : IBridgeActionTypeHandler, IBridgeActionTypeHandlerCallEndWork
	{
		[Import]
		private FLExConnectionHelper _connectionHelper;
		private bool _gotChanges;
		private string _fwProjectFolder;

		#region IBridgeActionTypeHandler impl

		public void StartWorking(Dictionary<string, string> options)
		{
			// As per the API, -p will be the main FW data file.
			// REVIEW (RandyR): What if it is the DB4o file?
			// REVIEW (RandyR): What is sent if the user is a client of the DB4o server?
			// -p <$fwroot>\foo\foo.fwdata
			_fwProjectFolder = Path.GetDirectoryName(options["-p"]);
			var pathToLiftProject = Utilities.LiftOffset(_fwProjectFolder);

			using (var chorusSystem = Utilities.InitializeChorusSystem(pathToLiftProject, options["-u"], LiftFolder.AddLiftFileInfoToFolderConfiguration))
			{
				if (chorusSystem.Repository.Identifier == null)
				{
					// First do a commit, since the repo is brand new.
					var projectConfig = chorusSystem.ProjectFolderConfiguration;
					ProjectFolderConfiguration.EnsureCommonPatternsArePresent(projectConfig);
					projectConfig.IncludePatterns.Add("**.ChorusRescuedFile");

					chorusSystem.Repository.AddAndCheckinFiles(projectConfig.IncludePatterns, projectConfig.ExcludePatterns, "Initial commit");
				}
				chorusSystem.EnsureAllNotesRepositoriesLoaded();

				// -p <$fwroot>\foo\foo.fwdata
				var origPathname = Path.Combine(pathToLiftProject, Path.GetFileNameWithoutExtension(LiftUtilties.PathToFirstLiftFile(_fwProjectFolder)) + LiftUtilties.LiftExtension);

				// Do the Chorus business.
				using (var syncDlg = (SyncDialog)chorusSystem.WinForms.CreateSynchronizationDialog(SyncUIDialogBehaviors.Lazy, SyncUIFeatures.NormalRecommended | SyncUIFeatures.PlaySoundIfSuccessful))
				{
					var syncAdjunt = new LiftSynchronizerAdjunct(origPathname);
					syncDlg.SetSynchronizerAdjunct(syncAdjunt);

					// Chorus does it in this order:
					// Local Commit
					// Pull
					// Merge (Only if anything came in with the pull from other sources, and commit of merged results)
					// Push
					syncDlg.SyncOptions.DoPullFromOthers = true;
					syncDlg.SyncOptions.DoMergeWithOthers = true;
					syncDlg.SyncOptions.DoSendToOthers = true;
					syncDlg.Text = Resources.SendReceiveView_DialogTitleLift;
					syncDlg.StartPosition = FormStartPosition.CenterScreen;
					syncDlg.BringToFront();
					syncDlg.ShowDialog();

					if (syncDlg.SyncResult.DidGetChangesFromOthers || syncAdjunt.WasUpdated)
					{
						_gotChanges = true;
					}
				}
			}
		}

		public ActionType SupportedActionType
		{
			get { return ActionType.SendReceiveLift; }
		}

		#endregion IBridgeActionTypeHandler impl

		#region IBridgeActionTypeHandlerCallEndWork impl

		public void EndWork()
		{
			_connectionHelper.SignalBridgeWorkComplete(_gotChanges);
		}

		#endregion IBridgeActionTypeHandlerCallEndWork impl
	}
}