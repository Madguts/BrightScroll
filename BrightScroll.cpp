#include <QApplication>
#include <QWidget>
#include <QVBoxLayout>
#include <QLabel>
#include <QSlider>
#include <QSystemTrayIcon>
#include <QMenu>
#include <QAction>
#include <QIcon>
#include <QPainter>
#include <QScreen>
#include <QGuiApplication>
#include <QMouseEvent>
#include <QCloseEvent>
#include <Windows.h>
#include <cmath>
#include <vector>
#include <string>
#include <highlevelmonitorconfigurationapi.h>
#include <physicalmonitorenumerationapi.h>

bool window_open = false;
QSystemTrayIcon *trayIcon = nullptr;

QIcon create_brightness_icon() {
    QPixmap pixmap(64, 64);
    pixmap.fill(Qt::transparent);
    QPainter painter(&pixmap);
    painter.setRenderHint(QPainter::Antialiasing);
    painter.setPen(QPen(Qt::white, 4));
    painter.drawEllipse(16, 16, 32, 32);
    for (int i = 0; i < 8; ++i) {
        int angle = i * 45;
        int x1 = 32 + 24 * std::cos(angle * M_PI / 180);
        int y1 = 32 + 24 * std::sin(angle * M_PI / 180);
        int x2 = 32 + 32 * std::cos(angle * M_PI / 180);
        int y2 = 32 + 32 * std::sin(angle * M_PI / 180);
        painter.drawLine(x1, y1, x2, y2);
    }
    return QIcon(pixmap);
}

void update_brightness(int value, const std::string &display, QLabel *label) {
    // Convert value to DWORD
    DWORD newValue = static_cast<DWORD>(value);

    // Get monitor handle
    HMONITOR hMonitor = MonitorFromWindow(nullptr, MONITOR_DEFAULTTOPRIMARY);
    DWORD numPhysicalMonitors;
    LPPHYSICAL_MONITOR physicalMonitors = nullptr;

    if (GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, &numPhysicalMonitors)) {
        physicalMonitors = (LPPHYSICAL_MONITOR)malloc(numPhysicalMonitors * sizeof(PHYSICAL_MONITOR));
        if (physicalMonitors != nullptr) {
            if (GetPhysicalMonitorsFromHMONITOR(hMonitor, numPhysicalMonitors, physicalMonitors)) {
                for (DWORD i = 0; i < numPhysicalMonitors; ++i) {
                    SetMonitorBrightness(physicalMonitors[i].hPhysicalMonitor, newValue);
                }
                DestroyPhysicalMonitors(numPhysicalMonitors, physicalMonitors);
            }
            free(physicalMonitors);
        }
    }

    label->setText(QString::fromStdString(display.substr(0, 5) + ": " + std::to_string(value) + "%"));
}

void on_mouse_wheel(QWheelEvent *event, QSlider *slider) {
    int delta = event->angleDelta().y() / 120; // 120 is the typical value for one notch
    slider->setValue(slider->value() + delta * 10);
}

void on_closing(QWidget *window) {
    window_open = false;
    window->close();
}

void on_clicked() {
    if (window_open) return;

    window_open = true;
    QWidget *window = new QWidget;
    window->setWindowTitle("Adjust Brightness");
    window->setWindowFlags(Qt::FramelessWindowHint);

    QVBoxLayout *layout = new QVBoxLayout(window);

    // Placeholder for monitor names
    std::vector<std::string> monitors = {"Monitor1", "Monitor2"};
    for (const auto &monitor : monitors) {
        // Placeholder for current brightness
        int current_brightness = 50;

        QLabel *label = new QLabel(QString::fromStdString(monitor.substr(0, 5) + ": " + std::to_string(current_brightness) + "%"));
        layout->addWidget(label);

        QSlider *slider = new QSlider(Qt::Vertical);
        slider->setRange(0, 100);
        slider->setValue(current_brightness);
        layout->addWidget(slider);

        QObject::connect(slider, &QSlider::valueChanged, [=](int value) { update_brightness(value, monitor, label); });
        slider->installEventFilter(new QObject([=](QObject *obj, QEvent *event) {
            if (event->type() == QEvent::Wheel) {
                on_mouse_wheel(static_cast<QWheelEvent *>(event), slider);
                return true;
            }
            return false;
        }));
    }

    window->setLayout(layout);
    window->show();

    QObject::connect(window, &QWidget::closeEvent, [=](QCloseEvent *event) { on_closing(window); });
}

int main(int argc, char *argv[]) {
    QApplication app(argc, argv);

    trayIcon = new QSystemTrayIcon(create_brightness_icon());
    QMenu *menu = new QMenu;
    QAction *adjustBrightnessAction = new QAction("Adjust Brightness");
    QAction *quitAction = new QAction("Quit");

    QObject::connect(adjustBrightnessAction, &QAction::triggered, on_clicked);
    QObject::connect(quitAction, &QAction::triggered, &app, &QApplication::quit);

    menu->addAction(adjustBrightnessAction);
    menu->addAction(quitAction);

    trayIcon->setContextMenu(menu);
    trayIcon->show();

    return app.exec();
}