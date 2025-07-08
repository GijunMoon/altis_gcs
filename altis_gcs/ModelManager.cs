using HelixToolkit.Wpf;
using System;
using System.Windows;
using System.Windows.Media.Media3D;
using System.Windows.Media;

namespace altis_gcs
{
    public class ModelManager
    {
        private readonly ModelVisual3D modelVisual;

        public ModelManager(ModelVisual3D modelVisual)
        {
            this.modelVisual = modelVisual;
            CreateAxisModel();
        }

        // X, Y, Z 축을 나타내는 모델 생성
        private void CreateAxisModel()
        {
            // Model3DGroup을 만들어 여러 3D 모델을 포함시킴
            var modelGroup = new Model3DGroup();

            // X축 (빨간색)
            var xAxisVisual = new LinesVisual3D();
            xAxisVisual.Points.Add(new Point3D(0, 0, 0));
            xAxisVisual.Points.Add(new Point3D(5, 0, 0));
            xAxisVisual.Thickness = 0.5;
            xAxisVisual.Color = Colors.Red;
            modelGroup.Children.Add(xAxisVisual.Content);

            // Y축 (녹색)
            var yAxisVisual = new LinesVisual3D();
            yAxisVisual.Points.Add(new Point3D(0, 0, 0));
            yAxisVisual.Points.Add(new Point3D(0, 5, 0));
            yAxisVisual.Thickness = 0.5;
            yAxisVisual.Color = Colors.Green;
            modelGroup.Children.Add(yAxisVisual.Content);

            // Z축 (파란색)
            var zAxisVisual = new LinesVisual3D();
            zAxisVisual.Points.Add(new Point3D(0, 0, 0));
            zAxisVisual.Points.Add(new Point3D(0, 0, 5));
            zAxisVisual.Thickness = 0.5;
            zAxisVisual.Color = Colors.Blue;
            modelGroup.Children.Add(zAxisVisual.Content);

            // X축 끝에 작은 구 추가 (좌표 표시)
            var xSphereVisual = new SphereVisual3D();
            xSphereVisual.Center = new Point3D(5, 0, 0);
            xSphereVisual.Radius = 0.2;
            xSphereVisual.Fill = new SolidColorBrush(Colors.Red);
            modelGroup.Children.Add(xSphereVisual.Content);

            // Y축 끝에 작은 구 추가
            var ySphereVisual = new SphereVisual3D();
            ySphereVisual.Center = new Point3D(0, 5, 0);
            ySphereVisual.Radius = 0.2;
            ySphereVisual.Fill = new SolidColorBrush(Colors.Green);
            modelGroup.Children.Add(ySphereVisual.Content);

            // Z축 끝에 작은 구 추가
            var zSphereVisual = new SphereVisual3D();
            zSphereVisual.Center = new Point3D(0, 0, 5);
            zSphereVisual.Radius = 0.2;
            zSphereVisual.Fill = new SolidColorBrush(Colors.Blue);
            modelGroup.Children.Add(zSphereVisual.Content);

            // 원점에 작은 구 추가
            var originSphereVisual = new SphereVisual3D();
            originSphereVisual.Center = new Point3D(0, 0, 0);
            originSphereVisual.Radius = 0.3;
            originSphereVisual.Fill = new SolidColorBrush(Colors.Yellow);
            modelGroup.Children.Add(originSphereVisual.Content);

            // 작은 그리드 평면 추가 (XY 평면)
            /*var gridVisual = new RectangleVisual3D();
            gridVisual.Origin = new Point3D(-3, -3, 0);
            gridVisual.Length = 6;
            gridVisual.Width = 6;
            gridVisual.Normal = new Vector3D(0, 0, 1);
            gridVisual.Fill = new SolidColorBrush(Color.FromArgb(100, 200, 200, 200));
            modelGroup.Children.Add(gridVisual.Content);*/

            // modelVisual에 모델 그룹 설정
            modelVisual.Content = modelGroup;
        }

        public void UpdateTransform(double roll, double pitch, double yaw)
        {
            double rollRad = roll * (Math.PI / 180.0);
            double pitchRad = pitch * (Math.PI / 180.0);
            double yawRad = yaw * (Math.PI / 180.0);

            // 각 축에 대한 쿼터니언 생성
            Quaternion qRoll = new Quaternion(new Vector3D(1, 0, 0), roll); // Roll (X축)
            Quaternion qPitch = new Quaternion(new Vector3D(0, 1, 0), pitch); // Pitch (Y축)
            Quaternion qYaw = new Quaternion(new Vector3D(0, 0, 1), yaw); // Yaw (Z축)

            // 쿼터니언 합성 (순서 중요: 일반적으로 Yaw -> Pitch -> Roll 순으로 곱함)
            Quaternion combinedQuaternion = qYaw * qPitch * qRoll;

            // 쿼터니언 회전 변환 생성
            var quaternionRotation = new QuaternionRotation3D(combinedQuaternion);

            // TransformGroup에 쿼터니언 회전 변환 추가
            var transformGroup = new Transform3DGroup();
            transformGroup.Children.Add(new RotateTransform3D(quaternionRotation));

            modelVisual.Transform = transformGroup;
        }

        public void UpdateTransformWithQuaternion(double q_w, double q_x, double q_y, double q_z)
        {
            // 센서에서 받은 쿼터니언 (실수-허수 순서: w, x, y, z)을
            // System.Windows.Media.Media3D.Quaternion 객체로 변환 (생성자 순서: x, y, z, w)
            Quaternion orientationQuaternion = new Quaternion(q_x, q_y, q_z, q_w);

            // 쿼터니언 회전 변환 생성
            var quaternionRotation = new QuaternionRotation3D(orientationQuaternion);

            // TransformGroup에 쿼터니언 회전 변환 추가
            var transformGroup = new Transform3DGroup();
            transformGroup.Children.Add(new RotateTransform3D(quaternionRotation));

            // 모델에 변환 적용
            if (modelVisual != null)
            {
                modelVisual.Transform = transformGroup;
            }
            else
            {
                Console.WriteLine("Warning: modelVisual is not initialized in ModelManager.");
            }
        }
    }
}
