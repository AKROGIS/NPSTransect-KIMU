using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ESRI.ArcGIS.Mobile.Client;
using ESRI.ArcGIS.Mobile.Client.Pages;

namespace AnimalObservations
{

    public class xxRecordObservationPage : EditFeatureAttributesPage
    {
        public xxRecordObservationPage()
        {
            InitializeComponent();
            // title
            this.Title = "Record Animal Observation";
            //subtitle
            this.Note = "Enter data about this observation";

            // page icon
            //Uri uri = new Uri("pack://application:,,,/AnimalObservations;Component/Tips72.png");
            //this.ImageSource = new System.Windows.Media.Imaging.BitmapImage(uri);

            // back button
            //this.BackCommands.Add(this.CancelCommand);


            // additional button
            PageNavigationCommand newObservationButton = new PageNavigationCommand(
                PageNavigationCommandType.Highlighted,
                "New Observation",
                param => newObservationCommandExecute(),
                param => { return true; }
                );

            // forward Buttons
            this.ForwardCommands.Clear();
            this.ForwardCommands.Add(newObservationButton);
            this.OkCommand.Text = "Save";
            this.ForwardCommands.Add(this.OkCommand);

        }

        protected override void OnBackCommandExecute()
        {
            ESRI.ArcGIS.Mobile.Client.Windows.MessageBox.ShowDialog("Removing Changes", "Ok");
            MobileApplication.Current.Transition(this.PreviousPage);
        }

        protected override void OnOkCommandExecute()
        {
            ESRI.ArcGIS.Mobile.Client.Windows.MessageBox.ShowDialog("Saving Changes", "Ok");
            MobileApplication.Current.Transition(this.PreviousPage);
        }
        private void newObservationCommandExecute()
        {
            //put current observation on the stack/queue
            //start a new observation
            //Start an observation record, launch new page
            ESRI.ArcGIS.Mobile.Client.Windows.MessageBox.ShowDialog("You are queueing up the observations", "Dude!");
            MobileApplication.Current.Transition(new xxRecordObservationPage());
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            ESRI.ArcGIS.Mobile.Client.Windows.MessageBox.ShowDialog("ArcGIS Mobile", "Hello");
        }


    }
}









namespace AnimalObservations
{

    public class junk
    {

        EditFeatureAttributesPage _editPage = new EditFeatureAttributesPage();

        //    private void SetupEditPage()
        //    {

        //        PageNavigationCommand SaveButton = new PageNavigationCommand(
        //PageNavigationCommandType.Positive,
        //"Save",
        //param => SaveCommandExecute(),
        //param => { return true; }
        //);

        //        PageNavigationCommand NewButton = new PageNavigationCommand(
        //PageNavigationCommandType.Highlighted,
        //"New Observation",
        //param => NewCommandExecute(),
        //param => { return true; }
        //);

        //        PageNavigationCommand BackButton = new PageNavigationCommand(
        //PageNavigationCommandType.Negative,
        //"Cancel",
        //param => BackCommandExecute(),
        //param => { return true; }
        //);

        //        // forward Buttons
        //        _editPage.BackCommands.Clear();
        //        _editPage.BackCommands.Add(BackButton);

        //        _editPage.MenuHeaderText = "Middle";
        //        // forward Buttons
        //        _editPage.ForwardCommands.Clear();
        //        _editPage.ForwardCommands.Add(NewButton);
        //        _editPage.ForwardCommands.Add(SaveButton);

        //        _editPage.Focusable = true;
        //        _editPage.Loaded += (s, e) => Keyboard.Focus(_editPage);
        //        _editPage.KeyDown += new KeyEventHandler(_editPage_KeyDown);
        //    }


        private void stuff()
        {
            //Start an observation record, launch new page
            //ESRI.ArcGIS.Mobile.Client.Windows.MessageBox.ShowDialog("Capturing a new observation at this point", "Super!");
            //o = new observation(currentGPSpoint);
            //display get observation attributes page(o)


            //
            //

            FeatureLayer fl = GetObservations();

            //FeatureDataTable table = fl.GetDataTable(null, null);
            //FeatureDataRow newRow = table.NewRow();
            //table.Rows.Add(newRow);

            //sets the new geometry to the geometry field

            // updates the feature layer data table
            //table.SaveInFeatureLayer();

            // get the domain values for the valvecondition
            //CodedValueDomain cvdomain1 = newRow.GetDomain("Species") as CodedValueDomain;
            //CodedValueDomain cvdomain2 = newRow.GetDomain("Behavior") as CodedValueDomain;


            //Feature newFeature = new Feature(newRow);
            //Collection<FeatureType> cft = MobileApplication.Current.Project.FeatureTypeDictionary[fl]
            FeatureType ft = MobileApplication.Current.Project.FeatureTypeDictionary[fl][0];
            int c = MobileApplication.Current.Project.FeatureTypeDictionary[fl].Count;
            Feature newFeature = new Feature(ft);

            newFeature.FeatureDataRow[fl.GeometryColumnIndex] = GetGeometry();
            CodedValueDomain cvdomain1 = newFeature.FeatureDataRow.GetDomain("Species") as CodedValueDomain;
            CodedValueDomain cvdomain2 = newFeature.FeatureDataRow.GetDomain("Behavior") as CodedValueDomain;
            newFeature.FeatureDataRow["Date_time"] = DateTime.Now;

            //ESRI.ArcGIS.Mobile.Client.Windows.MessageBox.ShowDialog("newfeature is " + (newFeature.IsEditing ? "editing" : "not editing"), "Super!");

            //RecordObservationPage _editPage = new RecordObservationPage();

            //_editPage.Feature = newFeature;
            //MobileApplication.Current.Transition(_editPage);
            //var page = new EditObservationAttributesPage(newFeature);
            //page.Feature = newFeature;
            //var stuff = page.EditableAttributes;
            //Console.WriteLine("Item Source = {0}", page.attributeListBox.ItemsSource);

            //ESRI.ArcGIS.Mobile.Client.Windows.MessageBox.ShowDialog("Attributes " + stuff.Count.ToString(), "Hello");

            //MobileApplication.Current.Transition(new EditObservationAttributesPage(newFeature));
            //MobileApplication.Current.Transition(page);

        }

        //protected override void OnPreviewKeyDown(KeyEventArgs e)
        //{
        //    //ESRI.ArcGIS.Mobile.Client.Windows.MessageBox.ShowDialog("Preview Key Down", "Hello");
        //    base.OnPreviewKeyDown(e);
        //}

        void _editPage_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                SaveCommandExecute();
            if (e.Key == Key.Escape)
                BackCommandExecute();
            if (e.Key == Key.Space)
                NewCommandExecute();
            //if (e.Key != Key.Escape && e.Key != Key.Enter && e.Key != Key.Space)
            //    ((EditFeatureAttributesPage)sender).base.OnKeyDown(e);
        }

        private object GetGeometry()
        {
            return null;
        }

        private FeatureLayer GetObservations()
        {
            foreach (FeatureLayerInfo f in MobileApplication.Current.Project.EnumerateFeatureLayerInfos())
            {
                if (f.Name == "Observations")
                {
                    //ESRI.ArcGIS.Mobile.Client.Windows.MessageBox.ShowDialog(f.FeatureLayer.GetFeatureCount().ToString(), "Found Transects");
                    return f.FeatureLayer;
                }
            }
            //ESRI.ArcGIS.Mobile.Client.Windows.MessageBox.ShowDialog("None", "Transects Not Found");
            return null;
            ;
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            ESRI.ArcGIS.Mobile.Client.Windows.MessageBox.ShowDialog("ArcGIS Mobile", "Hello");
        }

        void SaveCommandExecute()
        {
            ESRI.ArcGIS.Mobile.Client.Windows.MessageBox.ShowDialog("Saving Changes", "Ok");
            MobileApplication.Current.Transition(_editPage.PreviousPage);
        }

        void BackCommandExecute()
        {
            ESRI.ArcGIS.Mobile.Client.Windows.MessageBox.ShowDialog("Discarding Changes", "Ok");
            MobileApplication.Current.Transition(_editPage.PreviousPage);
        }

        void NewCommandExecute()
        {
            ESRI.ArcGIS.Mobile.Client.Windows.MessageBox.ShowDialog("Starting New Observation", "Ok");
            //MobileApplication.Current.Transition(_editPage.PreviousPage);
        }




    }
}
