using BasicRender.Engine;
using MathematicalEntities;
using PhysicsEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace BasicRender {

    public struct OneLineCacheStruct {
        public long data1;
        public long data2;
        public long data3;
        public long data4;
        public long data5;
        public long data6;
        public long data7;
        public long data8;
    }

    public struct pLine {
        public pLine(int x0, int y0, int x1, int y1) {
            this.x0 = x0;
            this.y0 = y0;
            this.x1 = x1;
            this.y1 = y1;
        }
        public int x0;
        public int y0;
        public int x1;
        public int y1;
    }

    public struct pBGRA {
        public pBGRA(int blue, int green, int red, int alpha) {
            this.blue = blue;
            this.green = green;
            this.red = red;
            this.alpha = alpha;
        }
        public int blue;
        public int green;
        public int red;
        public int alpha;
    }

    public partial class MainWindow : Window {

        private const int MAX_RAY_DEPTH = 5;
        private const float INFINITY = 100000000.0f;
        private const float M_PI = 3.141592653589793f;

        private GameTimer _timer = new GameTimer();

        private WriteableBitmap _wbStat;
        private Int32Rect _rectStat;
        private byte[] _pixelsStat;
        private int _strideStat;
        private int _pixelWidthStat;
        private int _pixelHeightStat;

        private WriteableBitmap _wb;
        private Int32Rect _rect;
        private byte[] _pixels;
        private int _stride;
        private int _pixelWidth;
        private int _pixelHeight;

        public MainWindow() {
            InitializeComponent();
        }

        static void printLine(byte[] buf, pLine lineCoords, pBGRA color, int pixelWidth) {

            int stride = (pixelWidth * 32) / 8;
            int pixelHeight = buf.Length / stride;

            int x0 = lineCoords.x0;
            int y0 = lineCoords.y0;
            int x1 = lineCoords.x1;
            int y1 = lineCoords.y1;

            int dx = Math.Abs(x1 - x0);
            int sx = x0 < x1 ? 1 : -1;

            int dy = Math.Abs(y1 - y0);
            int sy = y0 < y1 ? 1 : -1;

            int err = (dx > dy ? dx : -dy) / 2;
            int e2;

            for (; ; ) {

                if (!(x0 >= pixelWidth || y0 >= pixelHeight || x0 < 0 || y0 < 0))
                    printPixel(buf, x0, y0, color, pixelWidth);

                if (x0 == x1 && y0 == y1)
                    break;

                e2 = err;

                if (e2 > -dx) {
                    err -= dy;
                    x0 += sx;
                }

                if (e2 < dy) {
                    err += dx;
                    y0 += sy;
                }
            }
        }

        static void printPixel(byte[] buf, int x, int y, pBGRA color, int pixelWidth) {

            int blue = color.blue;
            int green = color.green;
            int red = color.red;
            int alpha = color.alpha;

            int pixelOffset = (x + y * pixelWidth) * 32 / 8;
            buf[pixelOffset] = (byte)blue;
            buf[pixelOffset + 1] = (byte)green;
            buf[pixelOffset + 2] = (byte)red;
            buf[pixelOffset + 3] = (byte)alpha;
        }

        static void fillScreen(byte[] buf, pBGRA color, int pixelWidth) {

            int stride = (pixelWidth * 32) / 8;
            int pixelHeight = buf.Length / stride;

            for (int y = 0; y < pixelHeight; y++)
                for (int x = 0; x < pixelWidth; x++)
                    printPixel(buf, x, y, color, pixelWidth);
        }

        static void lmoveScreen(byte[] buf, pBGRA fillColor, int moveAmt, int pixelWidth) {

            int stride = (pixelWidth * 32) / 8;
            int pixelHeight = buf.Length / stride;

            for (int y = 0; y < pixelHeight; y++) {
                for (int x = 0; x < pixelWidth; x++) {

                    int nextPixel = x + moveAmt;
                    if (nextPixel < pixelWidth) {
                        int pixelOffset = (nextPixel + y * pixelWidth) * 32 / 8;
                        printPixel(buf, x, y, new pBGRA(buf[pixelOffset], buf[pixelOffset + 1], buf[pixelOffset + 2], buf[pixelOffset + 3]), pixelWidth);
                    }
                    else {
                        printPixel(buf, x, y, fillColor, pixelWidth);
                    }
                }
            }
        }

        public float mix(float a, float b, float mix) {
            return b * mix + a * (1 - mix);
        }

        public Vec3f trace(Vec3f rayorig, Vec3f raydir, List<Sphere> spheres, int depth) {

            float tnear = INFINITY;
            Sphere sphere = null;

            //find intersection of this ray with the sphere in the scene
            int spCount = spheres.Count();
            for (int i = 0; i < spCount; ++i) {

                float t0 = INFINITY;
                float t1 = INFINITY;
                (bool, float, float) inter = spheres[i].intersect(rayorig, raydir);
                if (inter.Item1) {
                    t0 = inter.Item2;
                    t1 = inter.Item3;
                    if (t0 < 0) t0 = t1;
                    if (t0 < tnear) {
                        tnear = t0;
                        sphere = spheres[i];
                    }
                }
            }

            //if there's no intersection return black or background color
            if (sphere == null) return new Vec3f(1.0f);

            Vec3f surfaceColor = new Vec3f(0.0f);   //color of the ray/surfaceof the object intersected by the ray
            Vec3f phit = rayorig + raydir * tnear;  //point of intersection
            Vec3f nhit = phit - sphere.center;      //normal at the intersection point
            nhit.normalize();                       //normalize normal direction

            //If the normal and the view direction are not opposite to each other
            //reverse the normal direction. That also means we are inside the sphere so set
            //the inside bool to true. Finally reverse the sign of IdotN which we want
            //positive.
            float bias = 0.0001f; //add some bias to the point from which we will be tracing
            bool inside = false;
            if (raydir.dot(nhit) > 0) {
                nhit = -1 * nhit;
                inside = true;
            }

            if ((sphere.transparency > 0 || sphere.reflection > 0) && depth < MAX_RAY_DEPTH) {

                float facingratio = -raydir.dot(nhit);

                // change the mix value to tweak the effect
                float fresneleffect = mix((float)Math.Pow(1 - facingratio, 3), 1, 0.1f);

                // compute reflection direction (not need to normalize because all vectors
                // are already normalized)
                Vec3f refldir = raydir - nhit * 2 * raydir.dot(nhit);
                refldir.normalize();
                Vec3f reflection = trace(phit + nhit * bias, refldir, spheres, depth + 1);
                Vec3f refraction = new Vec3f(0.0f);

                // if the sphere is also transparent compute refraction ray (transmission)
                if (sphere.transparency != 0.0f) {

                    float ior = 1.1f;
                    float eta = (inside) ? ior : 1 / ior; // are we inside or outside the surface?
                    float cosi = -nhit.dot(raydir);
                    float k = 1 - eta * eta * (1 - cosi * cosi);
                    Vec3f refrdir = raydir * eta + nhit * (eta * cosi - (float)Math.Sqrt(k));
                    refrdir.normalize();
                    refraction = trace(phit - nhit * bias, refrdir, spheres, depth + 1);
                }

                // the result is a mix of reflection and refraction (if the sphere is transparent)
                surfaceColor = (reflection * fresneleffect + refraction * (1.0f - fresneleffect) * sphere.transparency) * sphere.surfaceColor;
            }
            else {

                // it's a diffuse object, no need to raytrace any further
                for (int i = 0; i < spCount; ++i) {
                    if (spheres[i].emissionColor.x > 0) {

                        // this is a light
                        Vec3f transmission = new Vec3f(1.0f);
                        Vec3f lightDirection = spheres[i].center - phit;
                        lightDirection.normalize();

                        for (int j = 0; j < spCount; ++j) {
                            if (i != j) {

                                if (spheres[j].intersect(phit + nhit * bias, lightDirection).Item1) {
                                    transmission = new Vec3f(0.0f);
                                    break;
                                }
                            }
                        }

                        surfaceColor += sphere.surfaceColor * transmission * Math.Max(0.0f, nhit.dot(lightDirection)) * spheres[i].emissionColor;
                    }
                }
            }

            return surfaceColor + sphere.emissionColor;
        }

        public class lineToRender {
            public int from;
            public int to;
            public byte[] buf;
        }

        public lineToRender[] lineTos;

        void render(byte[] buf, List<Sphere> spheres, int pixelWidth) {

            float tm = _timer.gameTime() * 2.0f;

            int stride = (pixelWidth * 32) / 8;
            int pixelHeight = buf.Length / stride;

            int width = pixelWidth;
            int height = pixelHeight;

            float invWidth = 1.0f / (float)width;
            float invHeight = 1.0f / (float)height;
            float fov = 80.0f;
            float aspectratio = width / (float)height;
            float angle = (float)Math.Tan(M_PI * 0.5f * fov / 180.0f);

            List<Task> tasks = new List<Task>();
            for (int i = 0; i < lineTos.Length; i++) {
                Task task = new Task(obj => {

                    // Trace rays
                    lineToRender toRender = lineTos[(int)obj];
                    for (int y = toRender.from; y < toRender.to; ++y) {
                        for (int x = 0; x < width; ++x) {

                            float xx = (2.0f * (((float)x + 0.5f) * invWidth) - 1.0f) * angle * aspectratio;
                            float yy = (1.0f - 2.0f * (((float)y + 0.5f) * invHeight)) * angle;
                            Vec3f raydir = new Vec3f(xx, yy, -1.0f);
                            raydir.normalize();

                            Vec3f raydir2 = new Vec3f(Mat4f.RotationYMatrix(-tm) * raydir);
                            //Vec3f raydir2 = raydir;

                            //Vec3f rayorig = new Vec3f(0.0f, 1.0f + (float)Math.Sin(tm) * 4.0f, -20.0f) + new Vec3f(Mat4f.RotationYMatrix(-tm) * new Vec3f(0.0f, 1.0f, 20.0f));
                            Vec3f rayorig = new Vec3f(0.0f, 5.0f, -25.0f) + new Vec3f(Mat4f.RotationYMatrix(-tm) * new Vec3f(0.0f, 5.0f, 25.0f));
                            //Vec3f rayorig = spheres[1].center + new Vec3f(Mat4f.RotationYMatrix(-tm) * new Vec3f(0.0f, 3.0f, 5.0f + tm));
                            //Vec3f rayorig = new Vec3f(0.0f);
                            //Vec3f rayorig = new Vec3f(0.0f, 10.0f, 0.0f);
                            Vec3f pixel = trace(rayorig, raydir2, spheres, 0);

                            printPixel(buf, x, y, new pBGRA((byte)(pixel.z * 255.0f), (byte)(pixel.y * 255.0f), (byte)(pixel.x * 255.0f), 255), pixelWidth);
                        }
                    }
                }, i);
                task.Start();
                tasks.Add(task);
            }
            
            foreach (Task t in tasks) {
                t.Wait();
            }
        }

        List<Sphere> _spheres = new List<Sphere>();
        private ParticleWorld _world1 = new ParticleWorld(300, 16);
        private Particlef _particle1;
        private Particlef _particle2;
        private Particlef _particle3;
        private Particlef _particle4;
        private Particlef _particle5;

        private Particlef _particle6;
        private Particlef _particle7;
        private Particlef _particle8;
        private Particlef _particle9;

        private ParticleGravity _g = new ParticleGravity(new Vec3f(0.0f, -9.8f * 4.0f, 0.0f));
        private SphereContact _sphereContactGenerator = new SphereContact();

        private void Window_Initialized(object sender, EventArgs e) {

            _timer.reset();
            _timer.start();

            _pixelWidth = (int)img.Width;
            _pixelHeight = (int)img.Height;

            int stride = (_pixelWidth * 32) / 8;

            lineTos = new lineToRender[] {
                new lineToRender() {from = 0, to = 16, buf = new byte[stride * 16]},
                new lineToRender() {from = 16, to = 32, buf = new byte[stride * 16]},
                new lineToRender() {from = 32, to = 48, buf = new byte[stride * 16]},
                new lineToRender() {from = 48, to = 64, buf = new byte[stride * 16]},
                new lineToRender() {from = 64, to = 80, buf = new byte[stride * 16]},
                new lineToRender() {from = 80, to = 96, buf = new byte[stride * 16]},
                new lineToRender() {from = 96, to = 112, buf = new byte[stride * 16]},
                new lineToRender() {from = 112, to = 128, buf = new byte[stride * 16]},
                new lineToRender() {from = 128, to = 144, buf = new byte[stride * 16]},
                new lineToRender() {from = 144, to = 160, buf = new byte[stride * 16]},
                new lineToRender() {from = 160, to = 176, buf = new byte[stride * 16]},
                new lineToRender() {from = 176, to = 192, buf = new byte[stride * 16]},
                new lineToRender() {from = 192, to = 200, buf = new byte[stride * 8]}
            };

            _wb = new WriteableBitmap(_pixelWidth, _pixelHeight, 96, 96, PixelFormats.Bgra32, null);
            _rect = new Int32Rect(0, 0, _pixelWidth, _pixelHeight);
            _pixels = new byte[_pixelWidth * _pixelHeight * _wb.Format.BitsPerPixel / 8];

            fillScreen(_pixels, new pBGRA(128, 128, 128, 255), _pixelWidth);
            printLine(_pixels, new pLine(0, 0, 64, 56), new pBGRA(195, 94, 65, 255), _pixelWidth);

            _stride = (_wb.PixelWidth * _wb.Format.BitsPerPixel) / 8;
            _wb.WritePixels(_rect, _pixels, _stride, 0);

            img.Source = _wb;

            // position, radius, surface color, reflectivity, transparency, emission color
            Sphere sphere1 = new Sphere(new Vec3f(0.0f, -10005, -20), 10000, new Vec3f(0.20f, 0.20f, 0.20f), 0, 0.0f);
            _spheres.Add(sphere1);
            _particle1 = new Particlef(sphere1, new Vec3f(0.0f), INFINITY);

            //красная
            Sphere sphere2 = new Sphere(new Vec3f(0.0f, 20, -20), 4.0f, new Vec3f(1.00f, 0.32f, 0.36f), 1, 0.9f);
            _spheres.Add(sphere2);
            _particle2 = new Particlef(sphere2, new Vec3f(0.0f), 10.0f);

            //желтая
            Sphere sphere3 = new Sphere(new Vec3f(5.0f, 10, -15), 2, new Vec3f(0.90f, 0.76f, 0.46f), 1, 0.9f);
            _spheres.Add(sphere3);
            _particle3 = new Particlef(sphere3, new Vec3f(0.0f), 10.0f);

            Sphere sphere4 = new Sphere(new Vec3f(5.0f, 10, -25), 3, new Vec3f(0.65f, 0.77f, 0.97f), 1, 0.9f);
            _spheres.Add(sphere4);
            _particle4 = new Particlef(sphere4, new Vec3f(0.0f), 10.0f);

            Sphere sphere5 = new Sphere(new Vec3f(-5.5f, 10, -15), 3, new Vec3f(0.90f, 0.90f, 0.90f), 1, 0.0f);
            _spheres.Add(sphere5);
            _particle5 = new Particlef(sphere5, new Vec3f(0.0f), 10.0f);


            //красная
            Sphere sphere6 = new Sphere(new Vec3f(0.0f, 40, -21), 6.0f, new Vec3f(0.92f, 0.36f, 0.26f), 1, 0.9f);
            _spheres.Add(sphere6);
            _particle6 = new Particlef(sphere6, new Vec3f(0.0f), 10.0f);

            //желтая
            Sphere sphere7 = new Sphere(new Vec3f(5.0f, 30, -15), 2, new Vec3f(0.76f, 0.72f, 0.36f), 1, 0.9f);
            _spheres.Add(sphere7);
            _particle7 = new Particlef(sphere7, new Vec3f(0.0f), 10.0f);

            Sphere sphere8 = new Sphere(new Vec3f(5.0f, 30, -25), 3, new Vec3f(0.65f, 0.77f, 0.97f), 1, 0.9f);
            _spheres.Add(sphere8);
            _particle8 = new Particlef(sphere8, new Vec3f(0.0f), 10.0f);

            Sphere sphere9 = new Sphere(new Vec3f(-5.5f, 30, -15), 3, new Vec3f(0.60f, 0.60f, 0.60f), 1, 0.9f);
            _spheres.Add(sphere9);
            _particle9 = new Particlef(sphere9, new Vec3f(0.0f), 10.0f);

            // light
            _spheres.Add(new Sphere(new Vec3f(20.0f, 30, -40), 3, new Vec3f(0.00f, 0.00f, 0.00f), 0, 0.0f, new Vec3f(3.0f, 3.0f, 3.0f)));

            _world1.getParticles().Add(_particle1);
            _world1.getParticles().Add(_particle2);
            _world1.getParticles().Add(_particle3);
            _world1.getParticles().Add(_particle4);
            _world1.getParticles().Add(_particle5);

            _world1.getParticles().Add(_particle6);
            _world1.getParticles().Add(_particle7);
            _world1.getParticles().Add(_particle8);
            _world1.getParticles().Add(_particle9);

            _sphereContactGenerator.init(_world1.getParticles());

            _world1.getForceRegistry().add(_particle1, _g);
            _world1.getForceRegistry().add(_particle2, _g);
            _world1.getForceRegistry().add(_particle3, _g);
            _world1.getForceRegistry().add(_particle4, _g);
            _world1.getForceRegistry().add(_particle5, _g);

            _world1.getForceRegistry().add(_particle6, _g);
            _world1.getForceRegistry().add(_particle7, _g);
            _world1.getForceRegistry().add(_particle8, _g);
            _world1.getForceRegistry().add(_particle9, _g);

            _world1.getContactGenerators().Add(_sphereContactGenerator);

            InitializeStats();

            CompositionTarget.Rendering += UpdateChildren;
        }

        private void InitializeStats() {

            _pixelWidthStat = (int)statImg.Width;
            _pixelHeightStat = (int)statImg.Height;

            _wbStat = new WriteableBitmap(_pixelWidthStat, _pixelHeightStat, 96, 96, PixelFormats.Bgra32, null);
            _rectStat = new Int32Rect(0, 0, _pixelWidthStat, _pixelHeightStat);
            _pixelsStat = new byte[_pixelWidthStat * _pixelHeightStat * _wbStat.Format.BitsPerPixel / 8];

            fillScreen(_pixelsStat, new pBGRA(32, 32, 32, 255), _pixelWidthStat);

            _strideStat = (_wbStat.PixelWidth * _wbStat.Format.BitsPerPixel) / 8;
            _wbStat.WritePixels(_rectStat, _pixelsStat, _strideStat, 0);

            statImg.Source = _wbStat;
        }

        private float _tt = 0.0f;
        private float _tt2 = 0.0f;
        private float _angle = 0.0f;
        Random _rnd = new Random();

        protected void UpdateChildren(object sender, EventArgs e) {

            RenderingEventArgs renderingArgs = e as RenderingEventArgs;
            _timer.tick();

            //Получить очередное (в данном случае - первое) случайное число
            float value1 = (float)((_rnd.Next() % 10) - 5);

            float duration = _timer.deltaTime();

            _tt += duration;
            if (_tt > 1.0f)
                _tt = 0.0f;

            _tt2 += duration;
            if (_tt2 > 5.0f) {
                _tt2 = 0.0f;

                _particle2.Position = new Vec3f(0.0f, 20.0f + value1, -20);
                _particle2.Velocity = new Vec3f(0.0f);

                _particle3.Position = new Vec3f(5.0f, 10.0f - value1, -15);
                _particle3.Velocity = new Vec3f(0.0f);

                _particle4.Position = new Vec3f(5.0f, 10.0f + value1, -25);
                _particle4.Velocity = new Vec3f(0.0f);

                _particle5.Position = new Vec3f(-5.5f, 10.0f - value1, -15);
                _particle5.Velocity = new Vec3f(0.0f);

                _particle6.Position = new Vec3f(0.0f, 40.0f + value1, -21);
                _particle6.Velocity = new Vec3f(0.0f);

                _particle7.Position = new Vec3f(5.0f, 30.0f - value1, -15);
                _particle7.Velocity = new Vec3f(0.0f);

                _particle8.Position = new Vec3f(5.0f, 30.0f + value1, -25);
                _particle8.Velocity = new Vec3f(0.0f);

                _particle9.Position = new Vec3f(-5.5f, 30.0f - value1, -15);
                _particle9.Velocity = new Vec3f(0.0f);
            }

            int blue = (int)(255.0 * _tt);
            int green = (int)(255.0 * _tt);
            int red = (int)(255.0 * _tt);

            //fillScreen(_pixels, new pBGRA(blue, green, red, 255), _pixelWidth);

            _world1.startFrame();
            _world1.runPhysics(duration);

            _spheres[0].center = _particle1.Position;
            _spheres[1].center = _particle2.Position;
            _spheres[2].center = _particle3.Position;
            _spheres[3].center = _particle4.Position;
            _spheres[4].center = _particle5.Position;

            _spheres[5].center = _particle6.Position;
            _spheres[6].center = _particle7.Position;
            _spheres[7].center = _particle8.Position;
            _spheres[8].center = _particle9.Position;

            render(_pixels, _spheres, _pixelWidth);

            Vec4f[] cube = new Vec4f[8] {
                new Vec4f(-1.0f,  1.0f, 1.0f, 0.0f),
                new Vec4f( 1.0f,  1.0f, 1.0f, 0.0f),
                new Vec4f( 1.0f, -1.0f, 1.0f, 0.0f),
                new Vec4f(-1.0f, -1.0f, 1.0f, 0.0f),
                new Vec4f(-1.0f,  1.0f, -1.0f, 0.0f),
                new Vec4f( 1.0f,  1.0f, -1.0f, 0.0f),
                new Vec4f( 1.0f, -1.0f, -1.0f, 0.0f),
                new Vec4f(-1.0f, -1.0f, -1.0f, 0.0f)
            };

            Mat4f model = new Mat4f(
                new Vec4f(1.0f, 0.0f, 0.0f, 0.0f),
                new Vec4f(0.0f, 1.0f, 0.0f, 0.0f),
                new Vec4f(0.0f, 0.0f, 1.0f, 0.0f),
                new Vec4f(0.0f, 0.0f, 0.0f, 1.0f)
            );

            model = model * Mat4f.RotationXMatrix(_angle);
            //model = model * Mat4f.RotationYMatrix(_angle);
            model = model * Mat4f.RotationZMatrix(_angle);

            Mat4f view = new Mat4f(
                new Vec4f(0.625f, 0.0f, 0.0f, 0.0f),
                new Vec4f(0.0f, 1.0f, 0.0f, 0.0f),
                new Vec4f(0.0f, 0.0f, 1.0f, 0.0f),
                new Vec4f(0.0f, 0.0f, 4.0f, 1.0f)
            );

            Mat4f modelView = model * view;

            Mat4f proj = Mat4f.ProjectionMatrix4(60.0f, 0.1f, 1000.0f);

            Vec4f point1 = modelView * cube[0];
            point1 = proj * point1;
            point1.x = (point1.x + 1.0f) / 2.0f * 320.0f;
            point1.y = (point1.y + 1.0f) / 2.0f * 200.0f;

            Vec4f point2 = modelView * cube[1];
            point2 = proj * point2;
            point2.x = (point2.x + 1.0f) / 2.0f * 320.0f;
            point2.y = (point2.y + 1.0f) / 2.0f * 200.0f;

            Vec4f point3 = modelView * cube[2];
            point3 = proj * point3;
            point3.x = (point3.x + 1.0f) / 2.0f * 320.0f;
            point3.y = (point3.y + 1.0f) / 2.0f * 200.0f;

            Vec4f point4 = modelView * cube[3];
            point4 = proj * point4;
            point4.x = (point4.x + 1.0f) / 2.0f * 320.0f;
            point4.y = (point4.y + 1.0f) / 2.0f * 200.0f;

            Vec4f point5 = modelView * cube[4];
            point5 = proj * point5;
            point5.x = (point5.x + 1.0f) / 2.0f * 320.0f;
            point5.y = (point5.y + 1.0f) / 2.0f * 200.0f;

            Vec4f point6 = modelView * cube[5];
            point6 = proj * point6;
            point6.x = (point6.x + 1.0f) / 2.0f * 320.0f;
            point6.y = (point6.y + 1.0f) / 2.0f * 200.0f;

            Vec4f point7 = modelView * cube[6];
            point7 = proj * point7;
            point7.x = (point7.x + 1.0f) / 2.0f * 320.0f;
            point7.y = (point7.y + 1.0f) / 2.0f * 200.0f;

            Vec4f point8 = modelView * cube[7];
            point8 = proj * point8;
            point8.x = (point8.x + 1.0f) / 2.0f * 320.0f;
            point8.y = (point8.y + 1.0f) / 2.0f * 200.0f;

            printLine(
                _pixels,
                new pLine(
                    (int)point1.x,
                    (int)point1.y,
                    (int)point2.x,
                    (int)point2.y
                ),
                new pBGRA(195, 94, 65, 255),
                _pixelWidth
            );

            printLine(
                _pixels,
                new pLine(
                    (int)point2.x,
                    (int)point2.y,
                    (int)point3.x,
                    (int)point3.y
                ),
                new pBGRA(195, 94, 65, 255),
                _pixelWidth
            );

            printLine(
                _pixels,
                new pLine(
                    (int)point3.x,
                    (int)point3.y,
                    (int)point4.x,
                    (int)point4.y
                ),
                new pBGRA(195, 94, 65, 255),
                _pixelWidth
            );

            printLine(
                _pixels,
                new pLine(
                    (int)point4.x,
                    (int)point4.y,
                    (int)point1.x,
                    (int)point1.y
                ),
                new pBGRA(195, 94, 65, 255),
                _pixelWidth
            );

            printLine(
                _pixels,
                new pLine(
                    (int)point5.x,
                    (int)point5.y,
                    (int)point6.x,
                    (int)point6.y
                ),
                new pBGRA(195, 94, 65, 255),
                _pixelWidth
            );

            printLine(
                _pixels,
                new pLine(
                    (int)point6.x,
                    (int)point6.y,
                    (int)point7.x,
                    (int)point7.y
                ),
                new pBGRA(195, 94, 65, 255),
                _pixelWidth
            );

            printLine(
                _pixels,
                new pLine(
                    (int)point7.x,
                    (int)point7.y,
                    (int)point8.x,
                    (int)point8.y
                ),
                new pBGRA(195, 94, 65, 255),
                _pixelWidth
            );

            printLine(
                _pixels,
                new pLine(
                    (int)point8.x,
                    (int)point8.y,
                    (int)point5.x,
                    (int)point5.y
                ),
                new pBGRA(195, 94, 65, 255),
                _pixelWidth
            );

            printLine(
                _pixels,
                new pLine(
                    (int)point1.x,
                    (int)point1.y,
                    (int)point5.x,
                    (int)point5.y
                ),
                new pBGRA(195, 94, 65, 255),
                _pixelWidth
            );

            printLine(
                _pixels,
                new pLine(
                    (int)point2.x,
                    (int)point2.y,
                    (int)point6.x,
                    (int)point6.y
                ),
                new pBGRA(195, 94, 65, 255),
                _pixelWidth
            );

            printLine(
                _pixels,
                new pLine(
                    (int)point3.x,
                    (int)point3.y,
                    (int)point7.x,
                    (int)point7.y
                ),
                new pBGRA(195, 94, 65, 255),
                _pixelWidth
            );

            printLine(
                _pixels,
                new pLine(
                    (int)point4.x,
                    (int)point4.y,
                    (int)point8.x,
                    (int)point8.y
                ),
                new pBGRA(195, 94, 65, 255),
                _pixelWidth
            );

            _angle += (float)(Math.PI / 32.0f);

            _wb.WritePixels(_rect, _pixels, _stride, 0);

            updateStats();
        }

        private void updateStats() {

            float duration = _timer.deltaTime();
            float totalTime = _timer.gameTime();
            int iduration = (int)(duration * 1000.0f);

            statsText.Text = $"RenderDuration: {duration * 1000.0f:F2}ms; FPS: {1.0f / duration:F0}; TotalTime: {totalTime:F3}sec";

            lmoveScreen(_pixelsStat, new pBGRA(32, 32, 32, 255), 1, _pixelWidthStat);
            if (iduration < 32)
                printPixel(_pixelsStat, 319, iduration, new pBGRA(0, 255, 0, 255), _pixelWidthStat);
            _wbStat.WritePixels(_rectStat, _pixelsStat, _strideStat, 0);
        }
    }
}
