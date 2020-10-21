#!/bin/bash

########################################
# Helpers
########################################

REGEX_SEMVER="^v([0-9]+)\.([0-9]+)\.([0-9]+)$"
REGEX_BRANCH="^[a-z/]+/(.*)$"
DIR_ROOT="$(git rev-parse --show-toplevel 2>/dev/null)"
CHANGELOG="CHANGELOG.md"

pushd . >/dev/null
SCRIPT_PATH="${BASH_SOURCE[0]}"
if ([ -h "${SCRIPT_PATH}" ]); then
	while ([ -h "${SCRIPT_PATH}" ]); do
		cd $(dirname "$SCRIPT_PATH")
		SCRIPT_PATH=$(readlink "${SCRIPT_PATH}")
	done
fi
cd $(dirname ${SCRIPT_PATH}) >/dev/null
SCRIPT_PATH=$(pwd)
popd >/dev/null

# Use in the the functions: eval $invocation
invocation='say_verbose "Calling: ${yellow:-}${FUNCNAME[0]} ${green:-}$*${normal:-}"'

# standard output may be used as a return value in the functions
# we need a way to write text on the screen in the functions so that
# it won't interfere with the return value.
# Exposing stream 3 as a pipe to standard output of the script itself
exec 3>&1

# Setup some colors to use. These need to work in fairly limited shells, like the Ubuntu Docker container where there are only 8 colors.
# See if stdout is a terminal
if [ -t 1 ]; then
	# see if it supports colors
	ncolors=$(tput colors)
	if [ -n "$ncolors" ] && [ $ncolors -ge 8 ]; then
		bold="$(tput bold || echo)"
		normal="$(tput sgr0 || echo)"
		black="$(tput setaf 0 || echo)"
		red="$(tput setaf 1 || echo)"
		green="$(tput setaf 2 || echo)"
		yellow="$(tput setaf 3 || echo)"
		blue="$(tput setaf 4 || echo)"
		magenta="$(tput setaf 5 || echo)"
		cyan="$(tput setaf 6 || echo)"
		white="$(tput setaf 7 || echo)"
	fi
fi

function say_err() {
	printf "%b\n" "${red:-}git-version: Error: $1${normal:-}" >&2
}

function say() {
	# using stream 3 (defined in the beginning) to not interfere with stdout of functions
	# which may be used as return value
	printf "%b\n" "${cyan:-}git-version:${normal:-} $1" >&3
}

function say_verbose() {
	if [ "$verbose" = true ]; then
		say "$1"
	fi
}

function say_set() {
	local varname="$1"
	local value="$2"

	say_verbose "${green:-}$varname${normal:-}=${yellow}$value${normal:-}"
}

# Joins elements in an array with a separator
# Takes a separator and array of elements to join
#
# Adapted from code by gniourf_gniourf (http://stackoverflow.com/a/23673883/1819350)
#
# Example
#   $ arr=("red car" "blue bike")
#   $ join " and " "${arr[@]}"
#   red car and blue bike
#   $ join $'\n' "${arr[@]}"
#   red car
#   blue bike
#
function join() {
	local separator=$1
	local elements=$2
	shift 2 || shift $(($#))
	printf "%s" "$elements${@/#/$separator}"
}

# Resolves a path to a real path
# Takes a string path
#
# Example
#   $ echo $(resolve-path "/var/./www/../log/messages.log")
#   /var/log/messages.log
#
function resolve-path() {
	local path="$1"
	if pushd "$path" >/dev/null 2>&1; then
		path=$(pwd -P)
		popd >/dev/null
	elif [ -L "$path" ]; then
		path="$(ls -l "$path" | sed 's#.* /#/#g')"
		path="$(resolve-path $(dirname "$path"))/$(basename "$path")"
	fi
	echo "$path"
}

function basename-git() {
	echo $(basename "$1" | tr '-' ' ' | sed 's/.sh$//g')
}

########################################
# Git version functions
########################################
function get-prev-version-tag() {
	eval $invocation

	local tag=$(git describe --tags --abbrev=0 --match="v[0-9]*" 2>/dev/null)
	if ! [[ $? -eq 0 ]]; then
		say_verbose "${magenta:-}No version tags found${normal:-}"
		echo ""
		return
	fi

	until [[ "$tag" =~ $REGEX_SEMVER ]]; do
		tag=$(git describe --tags --abbrev=0 --match="v[0-9]*" "$tag^" 2>/dev/null)
		if ! [[ $? -eq 0 ]]; then
			say_verbose "${magenta:-}No version tags found${normal:-}"
			echo ""
			return
		fi
	done

	echo "$tag"
}

function get-version-from-tag() {
	eval $invocation

	local tag="$1"
	if [[ "$tag" =~ $REGEX_SEMVER ]]; then
		local major=${BASH_REMATCH[1]}
		local minor=${BASH_REMATCH[2]}
		local patch=${BASH_REMATCH[3]}

		echo "$major.$minor.$patch"
		return
	fi

	say_err "$tag is not a valid version"
	exit 1
}

function get-exact-version-tag() {
	eval $invocation

	local tag=$(git describe --exact-match --tags --abbrev=0 --match="v[0-9]*" HEAD 2>/dev/null)
	if [[ $? -eq 0 ]]; then
		if [[ "$tag" =~ $REGEX_SEMVER ]]; then
			local major=${BASH_REMATCH[1]}
			local minor=${BASH_REMATCH[2]}
			local patch=${BASH_REMATCH[3]}

			echo "$major.$minor.$patch"
			return
		fi
	fi

	echo ""
}

function get-next-full-version() {
	eval $invocation

	local tag="$1"
	if [[ "$tag" =~ $REGEX_SEMVER ]]; then
		local major=${BASH_REMATCH[1]}
		local minor=${BASH_REMATCH[2]}
		local patch=${BASH_REMATCH[3]}

		local incrMajor=$(git rev-list --count --grep="Semver: major" $tag..HEAD)
		local incrMinor=$(git rev-list --count --grep="Semver: minor" $tag..HEAD)

		if [[ $incrMajor > 0 ]]; then
			say_verbose "incrementing major"
			major=$(($major + 1))
			minor=0
			patch=0
		elif [[ $incrMinor > 0 ]]; then
			say_verbose "incrementing minor"
			minor=$(($minor + 1))
			patch=0
		else
			patch=$(($patch + 1))
		fi

		echo "$major.$minor.$patch"
		return
	fi

	echo "0.1.0"
}

function get-branch-name() {
	eval $invocation

	git rev-parse --abbrev-ref HEAD
}

function get-branch-short-name() {
	eval $invocation

	local branch=$1

	if [[ "$branch" =~ $REGEX_BRANCH ]]; then
		branch=${BASH_REMATCH[1]}
	else
		say_verbose "did not match"
	fi

	echo "$branch"
}

function get-branch-point() {
	eval $invocation

	local branch=$1
	local base=$2
	if [ "$verbose" = true ]; then
		diff -u <(git rev-list --first-parent "$branch" 2>&3) <(git rev-list --first-parent "$base" 2>&3) | sed -ne 's/^ //p' | head -1
	else
		diff -u <(git rev-list --first-parent "$branch" 2>/dev/null ) <(git rev-list --first-parent "$base" 2>/dev/null) | sed -ne 's/^ //p' | head -1
	fi
}

function get-commit-count() {
	eval $invocation

	local since="$1"
	if [[ "$since" == "" ]]; then
		git rev-list --count HEAD
	else
		git rev-list --count "$since"..HEAD
	fi
}

function get-git-short-hash() {
	git rev-parse --short HEAD
}

function get-branch-version-meta() {
	eval $invocation

	local lastVersionTag="$1"

	local branchName=$(get-branch-name)
	say_set "branch-name" "$branchName"

	if [[ "$branchName" == "master" ]]; then
		local commitCount=$(get-commit-count "$lastVersionTag")
		say_set "commit-count" "$commitCount"
		echo "-ci.$commitCount"
		return
	fi

	local shortName=$(get-branch-short-name "$branchName")
	say_set "short-name" "$shortName"

	local branchPoint=$(get-branch-point "$branchName" "master")
	say_set "branch-point" "$branchPoint"

	local commitCount=$(get-commit-count "$branchPoint")
	say_set "commit-count" "$commitCount"

	local shortHash=$(get-git-short-hash)
	say_set "short-hash" "$shortHash"

	echo "-$shortName.$commitCount+$shortHash"
}

function get-version() {
	eval $invocation

	local exact=$(get-exact-version-tag)
	if ! [[ "$exact" == "" ]]; then
		say_verbose "Exact version match!"
		echo "$exact"
		return
	fi

	local lastVersionTag=$(get-prev-version-tag)
	say_set "last-version-tag" "$lastVersionTag"
	local nextVersion=$(get-next-full-version "$lastVersionTag")
	say_set "next-full-version" "$nextVersion"
	local branchMeta=$(get-branch-version-meta "$lastVersionTag")
	say_set "branch-meta" "$branchMeta"
	echo "$nextVersion$branchMeta"
}

function update-changelog() {
	local lastVersionTag=$(get-prev-version-tag)
	say_set "last-version-tag" "$lastVersionTag"
	local nextVersion=$(get-next-full-version "$lastVersionTag")
	say_set "next-full-version" "$nextVersion"

	local projectName=$(basename "$DIR_ROOT")
	echo "{\"name\":\"$projectName\",\"version\":\"$nextVersion\"}" >"$SCRIPT_PATH/package.json"

	npx -p "conventional-changelog-cli" conventional-changelog -k "$SCRIPT_PATH/package.json" -i ./CHANGELOG.md -s -r 0
	rm "$SCRIPT_PATH/package.json"
}

function create-version-tag() {
	local lastVersionTag=$(get-prev-version-tag)
	say_set "last-version-tag" "$lastVersionTag"
	local nextVersion=$(get-next-full-version "$lastVersionTag")
	say_set "next-full-version" "$nextVersion"

	git tag "v$nextVersion"
}

verbose=false

if [[ $# -eq 0 ]]; then
	echo "Use sub-command get or release"
	exit 1
fi

while [ $# -ne 0 ]; do
	name=$1
	case $name in
	--verbose)
		verbose=true
		;;

	get)
		get-version
		;;

	changelog)
		update-changelog
		;;

	tag)
		create-version-tag
		;;

	*)
		say_err "Unknown argument \"${red:-}$name${normal:-}\""
		;;
	esac
	shift
done
