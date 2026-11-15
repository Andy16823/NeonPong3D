# Neon Pong 3D — TODO

- [ ] Add first-class kinematic rigidbody support to the engine and use it for the moving paddles. They are currently mass-zero rigidbodies whose transforms are synchronized manually.
- [ ] Ensure moving kinematic bodies update their Bullet broadphase proxies and generate reliable contacts after each transform change.
- [ ] Add an engine example covering a moving paddle, a dynamic ball, contact callbacks, and render-target presentation to the backbuffer.
