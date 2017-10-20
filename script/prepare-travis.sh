if ! [[ "$TRAVIS_PULL_REQUEST" == "false" ]]; then
  git checkout -B "pr-$TRAVIS_PULL_REQUEST"
elif [[ "$(git rev-parse --abbrev-ref HEAD)" == "HEAD" ]]; then
  git checkout -B "$TRAVIS_BRANCH"
fi

if [[ "$(git rev-parse --abbrev-ref HEAD)" != "master" ]]; then
  git fetch origin master:master
fi
