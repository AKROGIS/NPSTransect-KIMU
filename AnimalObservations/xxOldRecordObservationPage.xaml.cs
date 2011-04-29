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

namespace AnimalObservations
{

    public partial class OldRecordObservationPage : MobileApplicationPage
    {
        public OldRecordObservationPage()
        {
            InitializeComponent();
            // title
            this.Title = "Record Animal Observation";
            //subtitle
            this.Note = "Enter data about this observation";

            // page icon
            Uri uri = new Uri("pack://application:,,,/AnimalObservations;Component/Tips72.png");
            this.ImageSource = new System.Windows.Media.Imaging.BitmapImage(uri);

            // back button
            this.BackCommands.Add(this.CancelCommand);

            // forward Button
            this.OkCommand.Text = "Save";
            this.ForwardCommands.Add(this.OkCommand);

            // additional button
            PageNavigationCommand newObservationButton = new PageNavigationCommand(
                PageNavigationCommandType.Highlighted,
                "New Observation",
                param => newObservationCommandExecute(),
                param => {return true;}
                );

            this.ForwardCommands.Add(newObservationButton);

        }

        protected override void OnBackCommandExecute()
        {
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
