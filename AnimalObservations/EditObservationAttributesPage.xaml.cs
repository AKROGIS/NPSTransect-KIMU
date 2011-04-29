using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using ESRI.ArcGIS.Mobile.Client;

namespace AnimalObservations
{

    public partial class EditObservationAttributesPage : MobileApplicationPage
    {
        public EditObservationAttributesPage()
        {

            InitializeData();

            InitializeComponent();

            this.Title = "Observation at " + Task.ActiveObservation.GpsPoint.LocalTime.ToLongTimeString();
            this.Note = "Edit the observation values";
            // page icon
            Uri uri = new Uri("pack://application:,,,/AnimalObservations;Component/Tips72.png");
            this.ImageSource = new System.Windows.Media.Imaging.BitmapImage(uri);

            PageNavigationCommand NewObservationCommand = new PageNavigationCommand(
                PageNavigationCommandType.Highlighted,
                "New Observation",
                param => NewObservationCommandExecute(),
                param => { return true; }  //We can always execute this command
            );

            this.OkCommand.CommandType = PageNavigationCommandType.Positive;
            this.OkCommand.Text = "Save";

            // back Buttons
            this.BackCommands.Clear();
            //FIXME - Cancel is more appropriate if this is an existing observation.
            this.CancelCommand.Text = "Delete";
            this.BackCommands.Add(CancelCommand);

            // forward Buttons
            this.ForwardCommands.Clear();
            this.ForwardCommands.Add(NewObservationCommand);
            this.ForwardCommands.Add(OkCommand);

            //Setup desired keyboard behavior
            this.Focusable = true;
            this.Loaded += (s, e) => Keyboard.Focus(this.angleTextBox);

            Task.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "ActiveObservation" && Task.ActiveObservation != null)
                    this.Title = "Observation at " + Task.ActiveObservation.GpsPoint.LocalTime.ToLongTimeString();
            };
        }

        private void InitializeData()
        {
            Task = MobileApplication.Current.FindTask(typeof(CollectTrackLogTask)) as CollectTrackLogTask;
            Debug.Assert(Task != null,
                "Fail!, Task is null in EditObservationAttributesPage");
            Debug.Assert(Task.ActiveObservation != null,
                "Fail!, Task.ActiveObservation is null in EditObservationAttributesPage");
            //FIXME - prove the above assertions are true for valid code and any data/userinput or add error checking

            //TODO: Consider using the feature attributes for handy property binding to tooltip, validation, etc.
            //FeatureAttribute Angle = Task.ActiveObservation.Feature.GetEditableAttributes()[0];

            BehaviorDomain = MobileUtilities.GetCodedValueDictionary<string>(BirdGroup.FeatureLayer, "Behavior");
            SpeciesDomain = MobileUtilities.GetCodedValueDictionary<string>(BirdGroup.FeatureLayer, "Species");
        }


        //These are public properties so that they are visible in XAML
        public CollectTrackLogTask Task { get; private set; }
        public IDictionary<string, string> BehaviorDomain { get; private set; }
        public IDictionary<string, string> SpeciesDomain { get; private set; }


        private BirdGroup2 birdGroupInProgress = new BirdGroup2();


        protected override void OnKeyDown(KeyEventArgs e)
        {
            //FIXME - Capture Tabs and don't let the user tab out of the content area.
            if (e.Key == Key.Enter)
                OnOkCommandExecute();
            if (e.Key == Key.Escape)
                OnBackCommandExecute();
            if (e.Key != Key.Escape && e.Key != Key.Enter)
            {
                base.OnKeyDown(e);
            }
            //See if this key helps define a bird group
            string keyString = (new KeyConverter()).ConvertToString(e.Key);
            if (keyString != null && keyString.Length > 0)
            {
                Char keyChar = keyString[0];
                if (!birdGroupInProgress.AcceptKey(keyChar))
                {
                    if (birdGroupInProgress.IsValid)
                    {
                        Task.ActiveObservation.BirdGroups.Add(birdGroupInProgress);
                        birdGroupInProgress = new BirdGroup2();
                    }
                    else
                    {
                        birdGroupInProgress.Reset();
                    }
                }
            }
            
        }
               
        protected override void OnCancelCommandExecute()
        {
            //Discard this observation
            //Transition to next in list, or if empty, previous page

            //FIXME - Are we editing an existing observation - discard changes, but do not delete observation
            //FIXME - if we are creating a new observation - delete newly create record.
 
            //ESRI.ArcGIS.Mobile.Client.Windows.MessageBox.ShowDialog("Delete observation", "Ok");
            //FIXME - if the observation has been saved (during creation?) then it should be deleted
            Task.RemoveObservation(Task.ActiveObservation);
            if (Task.ActiveObservation == null)
                MobileApplication.Current.Transition(this.PreviousPage);
        }

        protected override void OnOkCommandExecute()
        {
            //Save and close the current observation attribute page
            //Transition to next in list, or if empty, previous page

            //ESRI.ArcGIS.Mobile.Client.Windows.MessageBox.ShowDialog("Saving Changes", "Ok");
            Task.ActiveObservation.Save();
            Task.RemoveObservation(Task.ActiveObservation);
            if (Task.ActiveObservation == null)
                MobileApplication.Current.Transition(this.PreviousPage);
        }

        void NewObservationCommandExecute()
        {
            //Log observation point and add observation attribute page to the queue, do not change pages

            Task.AddObservation(Observation.CreateWith(Task.CurrentGpsPoint));
        }

        //FIXME capture the delete event in the data grid to properly dispose of the birdgroup. 
        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (gridView.SelectedIndex != -1)
            {
                BirdGroup2 bird = Task.ActiveObservation.BirdGroups[gridView.SelectedIndex];
                Task.ActiveObservation.BirdGroups.RemoveAt(gridView.SelectedIndex);
                bird.Delete();
            }
        }

        private void backButton_Click(object sender, RoutedEventArgs e)
        {
            Task.PreviousObservation();
        }

        private void forwardButton_Click(object sender, RoutedEventArgs e)
        {
            Task.NextObservation();
        }

    }
}
