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
using System.Windows.Shapes;
using System.Windows.Threading;

namespace SporeMaster
{
    /// <summary>
    /// Interaction logic for PleaseWait.xaml
    /// </summary>
    public partial class PleaseWait : Window
    {
        private bool cancelled = false;
        private List<double> subTaskScale = new List<double> { 1.0 };
        private List<double> subTaskEnd = new List<double> { 1.0 };

        public delegate void Operation(PleaseWait progress);

        public PleaseWait(Window parent, string operationDetails, Operation operation)
        {
            InitializeComponent();
            this.OperationDetails.Text = operationDetails;
            this.IsVisibleChanged +=new DependencyPropertyChangedEventHandler(PleaseWait_IsVisibleChanged);
            this.Owner = parent;
            this.ProgressBar.Maximum = 1.0;
            new System.Threading.Thread(delegate()
            {
                try
                {
                    operation(this);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception exc)
                {
                    MessageBox.Show(exc.ToString(), "Error");
                }
                complete();
            }).Start();
        }

        void  PleaseWait_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
 	        if (!this.IsVisible)
                this.cancelled = true;
        }

        public void beginTask(double progressInCurrentTask, double progressMaximum)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, (SimpleDelegate)delegate
            {
                double s = subTaskScale[subTaskScale.Count - 1];
                subTaskScale.Add(s * progressInCurrentTask / progressMaximum);
                subTaskEnd.Add(ProgressBar.Value + s * progressInCurrentTask);
            });
        }

        public void endTask()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, (SimpleDelegate)delegate
            {
                int i = subTaskScale.Count - 1;
                this.ProgressBar.Value = subTaskEnd[i];
                subTaskEnd.RemoveAt(i);
                subTaskScale.RemoveAt(i);
            });
        }

        public void addProgress(double amount)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, (SimpleDelegate)delegate
            {
                this.ProgressBar.Value += amount * this.subTaskScale[ subTaskScale.Count-1 ];
            });
            if (this.cancelled)
                throw new OperationCanceledException("Cancelled by user.");
        }

        public delegate void SimpleDelegate();

        /*public void addMaximum(double max)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal,  (SimpleDelegate)delegate
            {
                this.ProgressBar.Maximum += max;
            });
        }*/

        public void complete()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, (SimpleDelegate)delegate
            {
                this.Close();
            });
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.cancelled = true;
        }
    }
}
