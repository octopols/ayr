# Security

Ayr runs with administrator rights and writes to the embedded controller, so security problems matter more here than in
an ordinary app. Please report them privately: open the repository's **Security** tab and choose **Report a
vulnerability**. Don't open a public issue.

Include the Ayr version, your laptop model, and the steps that show the problem.

Ayr is designed to load no driver of its own and never write to the BIOS, battery or SMBus. Anything that breaks those
rules counts as a security issue.
