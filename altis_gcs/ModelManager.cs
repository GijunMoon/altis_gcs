using HelixToolkit.Wpf;
using System;
using System.Windows;
using System.Windows.Media.Media3D;

namespace altis_gcs
{
    public class ModelManager
    {
        private readonly ModelVisual3D _modelVisual;

        public ModelManager(ModelVisual3D modelVisual, string objFilePath)
        {
            _modelVisual = modelVisual;
            LoadObjFile(objFilePath);
            var initRollTransform = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 0), -90));
            _modelVisual.Transform = initRollTransform;
        }

        public void UpdateTransform(double roll, double pitch, double yaw)
        {
            var rollTransform = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 0), roll));
            var pitchTransform = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), pitch));
            var yawTransform = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 0, 1), yaw));

            var transformGroup = new Transform3DGroup();
            transformGroup.Children.Add(yawTransform);
            transformGroup.Children.Add(pitchTransform);
            transformGroup.Children.Add(rollTransform);

            _modelVisual.Transform = transformGroup;
        }

        private void LoadObjFile(string filePath)
        {
            var objReader = new ObjReader();
            try
            {
                var model = objReader.Read(filePath);
                _modelVisual.Content = model;
            }
            catch (Exception ex)
            {
                MessageBox.Show("OBJ 파일을 로드할 수 없습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}